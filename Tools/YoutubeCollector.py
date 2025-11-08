# ===========================================
# Unity 연동용 YouTube Data API (argparse 적용)
# - 키워드 입력 시 기간 필수
# 줄바꿈 제거
# ===========================================

from googleapiclient.discovery import build
from googleapiclient.errors import HttpError
import pandas as pd
import datetime
import time
import re
import argparse
import sys
import os

# ===========================================
# [1] API 설정
# ===========================================
API_KEY = "AIzaSyDiucMkEW5MfQbrExLa5CDop_34c0l98TU"
youtube = build("youtube", "v3", developerKey=API_KEY)

# ===========================================
# [2] 영상 ID 추출
# ===========================================
def extract_video_id(url):
    pattern = r"(?:v=|youtu\.be/)([a-zA-Z0-9_-]{11})"
    match = re.search(pattern, url)
    return match.group(1) if match else None

# ===========================================
# [3] 댓글 수집
# ===========================================
def get_video_comments(video_id, max_results=100):
    comments, next_page_token, collected = [], None, 0
    while collected < max_results:
        try:
            response = youtube.commentThreads().list(
                part="snippet",
                videoId=video_id,
                maxResults=min(100, max_results - collected),
                order="relevance",
                textFormat="plainText",
                pageToken=next_page_token
            ).execute()
            for item in response.get("items", []):
                snippet = item["snippet"]["topLevelComment"]["snippet"]
                comments.append({
                    "author": snippet["authorDisplayName"],
                    "text": snippet["textDisplay"].replace("\n", " ").strip(),
                    "like_count": snippet["likeCount"],
                    "published": snippet["publishedAt"]
                })
            collected += len(response.get("items", []))
            next_page_token = response.get("nextPageToken")
            if not next_page_token:
                break
        except HttpError:
            time.sleep(2)
            continue
    return comments

# ===========================================
# [4] 영상 세부 정보
# ===========================================
def get_video_info(video_id):
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
    except Exception:
        return None

# ===========================================
# [5] 날짜 제한 계산
# ===========================================
def get_published_after(period_type, amount):
    now = datetime.datetime.utcnow()
    if period_type == "year":
        delta = datetime.timedelta(days=amount * 365)
    elif period_type == "month":
        delta = datetime.timedelta(days=amount * 30)
    elif period_type == "week":
        delta = datetime.timedelta(weeks=amount)
    else:
        delta = datetime.timedelta(days=365)
    return (now - delta).isoformat("T") + "Z"

# ===========================================
# [6] 키워드 검색 후 영상/댓글 수집
# ===========================================
def get_video_info_basic(video_id):
    try:
        video_response = youtube.videos().list(
            part="snippet,statistics",
            id=video_id
        ).execute()

        if not video_response["items"]:
            return None

        item = video_response["items"][0]
        snippet = item["snippet"]
        stats = item.get("statistics", {})

        return {
            "video_title": snippet["title"],
            "video_link": f"https://www.youtube.com/watch?v={video_id}",
            "views": stats.get("viewCount", "0"),
            "likes": stats.get("likeCount", "0"),
            "published_at": snippet["publishedAt"],
            "thumbnail": snippet["thumbnails"]["high"]["url"],
        }
    except Exception:
        return None


def search_videos_with_comments(keyword, published_after=None, max_results=50):
    search_response = youtube.search().list(
        q=keyword,
        part="snippet",
        type="video",
        maxResults=max_results,
        publishedAfter=published_after
    ).execute()
    results = []
    for item in search_response["items"]:
        video_id = item["id"]["videoId"]
        info = get_video_info(video_id)
        if info:
            results.append(info)
    return results


def search_videos_basic(keyword, published_after=None, max_results=50):
    search_response = youtube.search().list(
        q=keyword,
        part="snippet",
        type="video",
        maxResults=max_results,
        publishedAfter=published_after
    ).execute()
    results = []
    for item in search_response["items"]:
        video_id = item["id"]["videoId"]
        info = get_video_info_basic(video_id)
        if info:
            results.append(info)
    return results

# ===========================================
# [7] CSV 저장
# ===========================================
if getattr(sys, "frozen", False):
    BASE_DIR = os.path.dirname(sys.executable)
else:
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))


def display_and_save_results(results, filename):
    filepath = os.path.join(BASE_DIR, filename)
    if not results:
        pd.DataFrame().to_csv(filepath, index=False, encoding="utf-8-sig")
        print(f"저장할 결과가 없습니다 → {filepath}")
        return

    if "comments" in results[0]:
        data_rows, comment_count = [], 0
        for r in results:
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
        pd.DataFrame(data_rows).to_csv(filepath, index=False, encoding="utf-8-sig")
        print(f"영상 {len(results)}개, 댓글 {comment_count}개 저장 완료 → {filepath}")
    else:
        pd.DataFrame(results).to_csv(filepath, index=False, encoding="utf-8-sig")
        print(f"영상 {len(results)}개 저장 완료 → {filepath}")

# ===========================================
# [8] main
# ===========================================
def main():
    parser = argparse.ArgumentParser(description="YouTube 영상/댓글 수집기 (Unity 연동용)")
    parser.add_argument("--text", type=str, help="검색 키워드 입력")
    parser.add_argument("--url", type=str, help="유튜브 영상 링크 입력")
    parser.add_argument("--period_type", choices=["year", "month", "week"], help="기간 단위 (year/month/week)")
    parser.add_argument("--amount", type=int, help="기간 수 (예: 3 → 3개월/3주/3년)")
    args = parser.parse_args()

    if args.url:
        video_id = extract_video_id(args.url)
        if not video_id:
            print("잘못된 URL입니다.")
            return
        info = get_video_info(video_id)
        if info:
            display_and_save_results([info], "youtube_link_results.csv")
        else:
            print("영상 정보를 불러오지 못했습니다.")

    elif args.text:
        if not args.period_type or not args.amount:
            print("키워드 검색에는 --period_type 과 --amount 인자가 필요합니다.")
            return
        published_after = get_published_after(args.period_type, args.amount)
        results = search_videos_basic(args.text, published_after=published_after)
        display_and_save_results(results, "youtube_keyword_results.csv")

    else:
        print("반드시 --text 또는 --url 중 하나를 입력해야 합니다.")

# ===========================================
# [9] 실행
# ===========================================
if __name__ == "__main__":
    main()