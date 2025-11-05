# ===========================================
# YouTube Data API 기반 영상 및 댓글 수집 (Unity 연동용 자동 실행 버전)
# - argparse로 명령행 인자 처리 (Unity에서 실행 가능)
# - 키워드 검색 또는 링크 직접 입력
# - 날짜 제한 필수 설정
# - 콘솔 출력: 영상 개수 + 제목만 표시
# - 결과 CSV 자동 저장
# ===========================================

# !pip install google-api-python-client pandas tqdm

import argparse
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError
import pandas as pd
import datetime
import time
import re
import sys

# ===========================================
# [1] API 연결 설정
# ===========================================
API_KEY = "AIzaSyDiucMkEW5MfQbrExLa5CDop_34c0l98TU"  # 🔑 본인 YouTube Data API 키 입력
youtube = build("youtube", "v3", developerKey=API_KEY)

# ===========================================
# [2] 유튜브 영상 ID 추출
# ===========================================
def extract_video_id(url):
    """유튜브 링크에서 video_id 추출"""
    pattern = r"(?:v=|youtu\.be/)([a-zA-Z0-9_-]{11})"
    match = re.search(pattern, url)
    return match.group(1) if match else None

# ===========================================
# [3] 댓글 수집 (인기순, 최대 100개)
# ===========================================
def get_video_comments(video_id, max_results=100):
    """특정 영상의 상위 댓글 수집"""
    comments = []
    next_page_token = None
    collected = 0
    retries = 3

    for attempt in range(retries):
        try:
            while collected < max_results:
                response = youtube.commentThreads().list(
                    part="snippet",
                    videoId=video_id,
                    maxResults=min(100, max_results - collected),
                    order="relevance",  # 인기순
                    textFormat="plainText",
                    pageToken=next_page_token
                ).execute()

                for item in response.get("items", []):
                    snippet = item["snippet"]["topLevelComment"]["snippet"]
                    comments.append({
                        "author": snippet["authorDisplayName"],
                        "text": snippet["textDisplay"],
                        "like_count": snippet["likeCount"],
                        "published": snippet["publishedAt"]
                    })

                collected += len(response.get("items", []))
                next_page_token = response.get("nextPageToken")
                if not next_page_token:
                    break
            break
        except HttpError as e:
            print(f"댓글 불러오기 실패 ({attempt+1}/{retries}) 재시도 중...")
            time.sleep(2)
    return comments

# ===========================================
# [4] 영상 세부 정보 수집
# ===========================================
def get_video_info(video_id):
    """영상의 기본 정보 + 댓글 수집"""
    retries = 3
    for attempt in range(retries):
        try:
            video_response = youtube.videos().list(
                part="snippet,statistics",
                id=video_id
            ).execute()

            if not video_response["items"]:
                return None

            item = video_response["items"][0]
            title = item["snippet"]["title"]
            thumbnail = item["snippet"]["thumbnails"]["high"]["url"]
            views = item["statistics"].get("viewCount", "0")
            likes = item["statistics"].get("likeCount", "0")
            published_at = item["snippet"]["publishedAt"]

            comments = get_video_comments(video_id, max_results=100)

            return {
                "video_id": video_id,
                "title": title,
                "thumbnail": thumbnail,
                "views": views,
                "likes": likes,
                "published_at": published_at,
                "link": f"https://www.youtube.com/watch?v={video_id}",
                "comments": comments
            }
        except HttpError:
            print(f"영상 정보 불러오기 실패 ({attempt+1}/{retries}) 재시도 중...")
            time.sleep(2)
    return None

# ===========================================
# [5] 날짜 제한 계산 함수
# ===========================================
def get_published_after(period_type, amount):
    """년/월/주 단위로 날짜 제한 계산"""
    now = datetime.datetime.utcnow()

    if period_type == "year":
        delta = datetime.timedelta(days=amount * 365)
    elif period_type == "month":
        delta = datetime.timedelta(days=amount * 30)
    elif period_type == "week":
        delta = datetime.timedelta(weeks=amount)
    else:
        print("❌ 잘못된 기간 단위입니다. 기본값(3년)으로 설정합니다.")
        delta = datetime.timedelta(days=3 * 365)

    return (now - delta).isoformat("T") + "Z"

# ===========================================
# [6] 키워드로 영상 검색
# ===========================================
def search_videos_with_comments(keyword, published_after=None, max_results=50):
    """키워드로 영상 검색 후 댓글 포함 수집"""
    print(f"\n🔍 '{keyword}' 키워드로 영상 검색 중...\n")

    search_response = youtube.search().list(
        q=keyword,
        part="snippet",
        type="video",
        maxResults=max_results,
        publishedAfter=published_after
    ).execute()

    results = []
    items = search_response["items"]
    
    for idx, item in enumerate(items, 1):
        if item["id"]["kind"] == "youtube#video":
            video_id = item["id"]["videoId"]
            info = get_video_info(video_id)
            if info:
                results.append(info)
    return results

# ===========================================
# [7] 결과 출력 및 CSV 저장
# ===========================================
def display_and_save_results(results, filename="youtube_results.csv"):
    """영상 제목만 출력 + 전체 데이터 CSV 저장"""
    data_rows = []
    comment_count = 0

    print("\n==============================")
    print(f"📺 수집된 영상 제목 목록 (총 {len(results)}개):\n")

    for idx, r in enumerate(results, start=1):
        print(f"{idx}. {r['title']}")
        for c in r["comments"]:
            comment_count += 1
            data_rows.append({
                "video_title": r["title"],
                "video_link": r["link"],
                "views": r["views"],
                "likes": r["likes"],
                "published_at": r["published_at"],
                "thumbnail": r["thumbnail"],
                "comment_author": c["author"],
                "comment_text": c["text"],
                "comment_likes": c["like_count"],
                "comment_published": c["published"]
            })

    df = pd.DataFrame(data_rows)
    df.to_csv(filename, index=False, encoding="utf-8-sig")

    print("\n==============================")
    print(f"✅ 총 영상 수집 개수: {len(results)}개")
    print(f"✅ 총 댓글 수집 개수: {comment_count}개")
    print(f"💾 '{filename}' 파일로 저장 완료!")
    print("==============================\n")

# ===========================================
# [8] main 실행 (Interactive Colab Version)
# - Unity 연동 시에는 이 부분을 주석 처리하고 스크립트 실행
# ===========================================
def main():
    """Unity에서 실행 가능한 메인 함수"""
    parser = argparse.ArgumentParser(description="YouTube 영상 및 댓글 수집기 (Unity 연동용)")
    parser.add_argument("--mode", choices=["keyword", "link"], required=True, help="검색 모드 선택 (keyword / link)")
    parser.add_argument("--text", type=str, help="검색 키워드 (mode=keyword일 때)")
    parser.add_argument("--url", type=str, help="유튜브 영상 링크 (mode=link일 때)")
    parser.add_argument("--period_type", choices=["year", "month", "week"], required=True, help="기간 단위 선택")
    parser.add_argument("--amount", type=int, required=True, help="기간 수 (예: 3 → 3개월/3년/3주)")
    parser.add_argument("--output", type=str, default="youtube_results.csv", help="결과 CSV 저장 경로")

    # 실제 명령줄 인자 파싱 (sys.argv 사용)
    args = parser.parse_args()

    published_after = get_published_after(args.period_type, args.amount)

    if args.mode == "keyword":
        if not args.text:
            print("❌ --text 인자가 설정되지 않았습니다.")
            sys.exit(1)
        results = search_videos_with_comments(args.text, published_after=published_after)
        display_and_save_results(results, args.output)

    elif args.mode == "link":
        if not args.url:
            print("❌ --url 인자가 설정되지 않았습니다.")
            sys.exit(1)
        video_id = extract_video_id(args.url)
        if not video_id:
            print("❌ 영상 ID를 찾을 수 없습니다.")
            sys.exit(1)
        info = get_video_info(video_id)
        if info:
            display_and_save_results([info], args.output)
        else:
            print("❌ 영상 정보를 불러오지 못했습니다.")

# ===========================================
# 실행 파트 (Colab 환경에서는 main_colab() 실행)
# - Unity 연동 시에는 이 부분을 수정하여 main() 실행
# ===========================================
if __name__ == "__main__":
    main()