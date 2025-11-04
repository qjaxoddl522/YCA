import os
import re
import sys
import csv
import argparse
from collections import Counter
from urllib.parse import urlparse

# ---------- 입력 소스 결정 ----------
def read_input_text(args):
    # 1) --text가 가장 우선
    if args.text:
        return args.text

    # 2) --input-file가 있으면 파일에서 읽기
    if args.input_file:
        with open(args.input_file, "r", encoding="utf-8") as f:
            return f.read()

    # 3) 환경변수
    env_text = os.environ.get("INPUT_TEXT")
    if env_text:
        return env_text

    # 4) STDIN (파이프/리다이렉트)
    if not sys.stdin.isatty():
        return sys.stdin.read()

    # 5) 아무 것도 없으면 빈 문자열
    return ""

# ---------- URL 탐지 ----------
URL_RE = re.compile(r"https?://[^\s]+")

def extract_urls(text):
    # http/https 링크만 추출
    return URL_RE.findall(text or "")

# ---------- 링크 CSV ----------
def write_links_csv(urls, out_path):
    rows = []
    for u in urls:
        parsed = urlparse(u)
        rows.append({
            "url": u,
            "scheme": parsed.scheme,
            "host": parsed.netloc,
            "path": parsed.path,
            "query": parsed.query,
            "fragment": parsed.fragment
        })

    # 엑셀 호환을 위해 utf-8-sig 권장
    with open(out_path, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.DictWriter(f, fieldnames=["url", "scheme", "host", "path", "query", "fragment"])
        writer.writeheader()
        writer.writerows(rows)

# ---------- 단어 빈도 CSV ----------
WORD_RE = re.compile(r"[A-Za-z0-9가-힣_]+")

def write_wordfreq_csv(text, out_path):
    words = WORD_RE.findall(text or "")
    
    normalized = [w.lower() for w in words]
    counter = Counter(normalized)

    rows = [{"word": w, "count": c, "length": len(w)} for w, c in counter.most_common()]

    with open(out_path, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.DictWriter(f, fieldnames=["word", "count", "length"])
        writer.writeheader()
        writer.writerows(rows)

# ---------- 메인 ----------
def main():
    p = argparse.ArgumentParser(description="입력에 링크가 있으면 링크 CSV, 없으면 단어 빈도 CSV를 생성합니다.")
    p.add_argument("--text", help="직접 입력 문자열")
    p.add_argument("--input-file", help="입력 텍스트 파일 경로")
    p.add_argument("--output", help="CSV 출력 경로(미지정 시 환경변수 OUTPUT_CSV 또는 ./output.csv)")
    args = p.parse_args()

    # 출력 경로 결정: --output > env(OUTPUT_CSV) > ./output.csv
    out_path = args.output or os.environ.get("OUTPUT_CSV") or "output.csv"
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)

    text = read_input_text(args)
    urls = extract_urls(text)

    if urls:
        write_links_csv(urls, out_path)
        print(f"[make_csv] 링크 {len(urls)}개를 분석하여 CSV 저장: {out_path}")
    else:
        write_wordfreq_csv(text, out_path)
        print(f"[make_csv] 단어 빈도 CSV 저장: {out_path}")

if __name__ == "__main__":
    main()
