# ===============================================
# 📘 YouTube CSV 자동 분석기 (NER + 감정분석 통합)
# - KoBERT+CRF 개체명 인식 (선택)
# - KoBERT/KcELECTRA 감정 분석
# - 댓글/제목 CSV 자동 판별 및 분석
# ===============================================

import os, re, argparse
import pandas as pd
import numpy as np
import torch
from torch import nn
from datetime import datetime
from collections import Counter
from tqdm import tqdm
from transformers import AutoTokenizer, AutoModel, AutoModelForSequenceClassification

tqdm.pandas()
device = "cuda" if torch.cuda.is_available() else "cpu"

# ------------------------------
# 🧩 Args 설정
# ------------------------------
class Args:
    def __init__(self, output=None, use_ner=False, sentiment_model=None,
                 neutral_threshold=0.60, min_len=3, topn=3):
        self.output = output
        self.use_ner = use_ner
        self.sentiment_model = sentiment_model
        self.neutral_threshold = neutral_threshold
        self.min_len = min_len
        self.topn = topn

# ------------------------------
# 🧠 감정 분석 모델 로드
# ------------------------------
def load_sentiment_model(model_choice=None):
    if model_choice == "kobert":
        name = "nlpai-lab/kobert-base-sentiment"
    elif model_choice == "kcelectra":
        name = "beomi/KcELECTRA-base-v2022"
    else:
        name = "nlpai-lab/kobert-base-sentiment"

    try:
        tok = AutoTokenizer.from_pretrained(name)
        model = AutoModelForSequenceClassification.from_pretrained(name).to(device).eval()
        label_map = {0: "부정", 1: "중립", 2: "긍정"} if model.config.num_labels == 3 else {0: "부정", 1: "긍정"}
        return tok, model, label_map
    except Exception:
        tok = AutoTokenizer.from_pretrained("beomi/KcELECTRA-base-v2022")
        model = AutoModelForSequenceClassification.from_pretrained("beomi/KcELECTRA-base-v2022").to(device).eval()
        label_map = {0: "부정", 1: "긍정"}
        return tok, model, label_map

# ------------------------------
# 🧩 NER (BERT+CRF)
# ------------------------------
def build_ner_extractor(use_ner=True):
    if not use_ner or not os.path.exists("kobert_crf_ner.pt"):
        print("NER 모델 비활성화 — 정규식 기반 키워드 추출 사용")
        return extract_keywords_regex

    try:
        from torchcrf import CRF
    except ImportError:
        print("torchcrf 미설치 — NER 비활성화, 정규식 사용")
        return extract_keywords_regex

    class BertCRF(nn.Module):
        def __init__(self, bert, num_labels):
            super().__init__()
            self.bert = bert
            self.dropout = nn.Dropout(0.1)
            self.fc = nn.Linear(bert.config.hidden_size, num_labels)
            self.crf = CRF(num_labels)

        def decode_tags(self, input_ids, attention_mask):
            with torch.no_grad():
                outputs = self.bert(input_ids, attention_mask=attention_mask)[0]
                emissions = self.fc(self.dropout(outputs)).transpose(0, 1)
                mask_t = attention_mask.transpose(0, 1).bool()
                preds = self.crf.decode(emissions, mask=mask_t)
                return preds

    id2label = {0: "O", 1: "B-ORG", 2: "I-ORG", 3: "B-LOC", 4: "I-LOC"}
    num_labels = len(id2label)
    tokenizer_ner = AutoTokenizer.from_pretrained("skt/kobert-base-v1")
    bert = AutoModel.from_pretrained("skt/kobert-base-v1")

    model_ner = BertCRF(bert, num_labels).to(device)
    model_ner.load_state_dict(torch.load("kobert_crf_ner.pt", map_location=device))
    model_ner.eval()
    print("KoBERT+CRF NER 모델 로드 완료")

    def extract_keywords_ner(text):
        tokens = re.findall(r"[가-힣a-zA-Z0-9]{2,}", str(text))
        if not tokens:
            return ""
        max_tokens = 256
        if len(tokens) > max_tokens:
            tokens = tokens[:max_tokens]
        enc = tokenizer_ner(
            tokens,
            is_split_into_words=True,
            return_tensors="pt",
            truncation=True,
            padding=True,
            max_length=512,
        )
        input_ids = enc["input_ids"].to(device)
        mask = enc["attention_mask"].to(device)
        preds = model_ner.decode_tags(input_ids, mask)
        if isinstance(preds, torch.Tensor):
            preds = preds.tolist()
        if isinstance(preds[0], torch.Tensor):
            preds = [p.tolist() for p in preds]
        if isinstance(preds[0], int):
            tags = preds[:len(tokens)]
        else:
            tags = preds[0][:len(tokens)]
        ents = [tok for tok, tid in zip(tokens, tags) if id2label[tid] != "O"]
        return " | ".join(ents[:3])

    return extract_keywords_ner

# ------------------------------
# 🧹 댓글 필터 & 키워드 추출
# ------------------------------
def is_spam_or_timeline(text, min_len=3):
    if not isinstance(text, str):
        return True
    text = text.strip().lower()
    if len(text) < min_len:
        return True
    if re.search(r"\b\d{1,2}:\d{2}(?::\d{2})?\b", text):
        return True
    if re.search(r"https?://|www\.|bit\.ly|\.com|\.net|\.io|구독|클릭|링크|이벤트|추천코드|할인", text):
        return True
    return False

def extract_keywords_regex(text, top_n=3):
    words = re.findall(r"[가-힣a-zA-Z0-9]{2,}", str(text))
    if not words:
        return ""
    freq = Counter(words)
    return " | ".join([w for w, _ in freq.most_common(top_n)])

def softmax_np(x):
    e = np.exp(x - np.max(x))
    return e / e.sum(axis=-1, keepdims=True)

# ------------------------------
# 💬 감정 예측기 빌드
# ------------------------------
def build_sentiment_predictor(model_choice=None, neutral_threshold=0.6):
    tok, model, label_map = load_sentiment_model(model_choice)

    positive_words = ["좋", "최고", "감동", "재밌", "멋", "대박", "굿", "짱", "👍", "ㅋㅋ", "ㅎㅎ", "예쁘", "귀엽", "감사", "고맙", "사랑", "완벽", "행복", "ㅠㅠ", "ㅜㅜ"]
    negative_words = ["싫", "별로", "이상", "최악", "문제", "버그", "실망", "없", "아니", "화남", "짜증", "에러", "불편", "개판"]

    def predict(text):
        if not isinstance(text, str) or len(text.strip()) < 2:
            return "중립"
        inputs = tok(text, return_tensors="pt", truncation=True, padding=True).to(device)
        with torch.no_grad():
            logits = model(**inputs).logits.cpu().numpy()[0]
        probs = softmax_np(logits)
        pred = probs.argmax()
        conf = probs.max()
        label = label_map.get(pred, "중립")

        if conf < neutral_threshold:
            if any(w in text for w in positive_words):
                label = "긍정"
            elif any(w in text for w in negative_words):
                label = "부정"
            else:
                label = "중립"
        if any(w in text for w in positive_words):
            label = "긍정"
        elif any(w in text for w in negative_words):
            label = "부정"
        if ("감사" in text or "고맙" in text) and ("ㅠ" in text or "ㅜ" in text):
            label = "긍정"
        return label

    return predict

# ------------------------------
# 🧾 CSV 분석 함수
# ------------------------------
def analyze_keyword_csv(input_csv, output_csv):
    df = pd.read_csv(input_csv)
    if "video_title" not in df.columns:
        print("video_title 열이 없습니다.")
        return
    df["published_at"] = pd.to_datetime(df.get("published_at", datetime.now()), errors="coerce").fillna(datetime.now())
    df["date"] = df["published_at"].dt.date
    records = []
    for _, r in df.iterrows():
        words = re.findall(r"[가-힣a-zA-Z0-9]{2,}", str(r["video_title"]))
        for w, c in Counter(words).items():
            records.append({"keyword": w, "date": r["date"], "count": c})
    result = pd.DataFrame(records).groupby(["keyword", "date"])["count"].sum().reset_index()
    result.to_csv(output_csv, index=False, encoding="utf-8-sig")
    print(f"'{output_csv}' 생성 완료 ({len(result)}행)")

def analyze_link_csv(input_csv, output_csv, args):
    df = pd.read_csv(input_csv)
    if "comment_text" not in df.columns:
        print("comment_text 열이 없습니다.")
        return
    if "comment_published" in df.columns:
        df["comment_published"] = pd.to_datetime(df["comment_published"], errors="coerce").fillna(datetime.now())
    else:
        df["comment_published"] = datetime.now()
    df.sort_values("comment_published", inplace=True)
    df = df[~df["comment_text"].apply(lambda x: is_spam_or_timeline(x, args.min_len))].reset_index(drop=True)
    print(f"필터링 후 남은 댓글 수: {len(df)}개")

    extract_keywords = build_ner_extractor(args.use_ner)
    predict_sent = build_sentiment_predictor(args.sentiment_model, args.neutral_threshold)

    df["keyword"] = df["comment_text"].progress_apply(lambda x: extract_keywords(x))
    df["sentiment"] = df["comment_text"].progress_apply(predict_sent)

    result = df[["comment_text", "comment_published", "keyword", "sentiment"]].rename(
        columns={"comment_text": "comment", "comment_published": "time"}
    )
    result.to_csv(output_csv, index=False, encoding="utf-8-sig")
    print(f"'{output_csv}' 생성 완료 ({len(result)}행)")

def auto_analyze_csv(input_csv, args):
    if not os.path.exists(input_csv):
        print(f"❌ 파일 없음: {input_csv}")
        return
    df = pd.read_csv(input_csv, nrows=5)
    if "comment_text" in df.columns:
        print("링크(댓글) CSV로 판별됨")
        analyze_link_csv(input_csv, args.output or "analyzed_comments.csv", args)
    elif "video_title" in df.columns:
        print("키워드(제목) CSV로 판별됨")
        analyze_keyword_csv(input_csv, args.output or "analyzed_keywords.csv")
    else:
        print("CSV 유형을 판별할 수 없습니다.")

# ------------------------------
# 🚀 실행
# ------------------------------
def parse_cli_args():
    parser = argparse.ArgumentParser(description="YouTube CSV 자동 분석기 (NER + 감정분석)")
    parser.add_argument("--input", type=str, help="분석할 CSV 파일 경로")
    parser.add_argument("--output", type=str, help="결과 CSV 저장 경로")
    parser.add_argument("--disable_ner", action="store_true", help="NER 비활성화 (정규식 기반 키워드 추출 사용)")
    parser.add_argument("--sentiment_model", choices=["kobert", "kcelectra"], default="kcelectra", help="감정 분석 모델 선택")
    parser.add_argument("--neutral_threshold", type=float, default=0.6, help="중립 판별 임계값")
    parser.add_argument("--min_len", type=int, default=3, help="댓글 최소 길이 (스팸/타임라인 필터)")
    parser.add_argument("--topn", type=int, default=3, help="키워드 상위 N개 추출")
    return parser.parse_args()


def resolve_path(base_dir, path):
    if not path:
        return None
    return path if os.path.isabs(path) else os.path.abspath(os.path.join(base_dir, path))


def main():
    base_dir = os.path.dirname(os.path.abspath(__file__))
    cli_args = parse_cli_args()

    input_csv = resolve_path(base_dir, cli_args.input)
    if not input_csv:
        candidate_files = [
            os.path.join(base_dir, "youtube_link_results.csv"),
            os.path.join(base_dir, "youtube_keyword_results.csv"),
        ]
        input_csv = next((path for path in candidate_files if os.path.exists(path)), None)
        if not input_csv:
            print("분석할 CSV 파일을 찾을 수 없습니다.")
            return
    elif not os.path.exists(input_csv):
        print(f"파일 없음: {input_csv}")
        return

    args = Args(
        output=resolve_path(base_dir, cli_args.output),
        use_ner=not cli_args.disable_ner,
        sentiment_model=cli_args.sentiment_model,
        neutral_threshold=cli_args.neutral_threshold,
        min_len=cli_args.min_len,
        topn=cli_args.topn,
    )

    auto_analyze_csv(input_csv, args)


if __name__ == "__main__":
    main()