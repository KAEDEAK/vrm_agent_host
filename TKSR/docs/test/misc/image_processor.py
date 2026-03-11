#!/usr/bin/env python3
"""
Image Processor - 画像送信ツール

画像ファイルをbase64エンコードしてサーバーに送信します。
リトライ、タイムアウト、画像リサイズ機能を提供します。

Usage:
    python image_processor.py -host 192.168.1.73:34560 -file test_image.png -retry yes -retryMax 5 -retryInterval 1000 -retryTimeout 1000 -w 640 -h 480
"""

import sys
import argparse
import base64
import time
from pathlib import Path
from typing import Optional, Tuple
import urllib.request
import urllib.error
import json

try:
    from PIL import Image
except ImportError:
    print("Error: PIL (Pillow) is required. Install with: pip install Pillow")
    sys.exit(1)


def load_and_resize_image(file_path: str, width: Optional[int] = None, height: Optional[int] = None) -> Tuple[bytes, int, int]:
    """
    画像を読み込み、必要に応じてリサイズします。

    Args:
        file_path: 画像ファイルパス
        width: 目標幅（ピクセル）、Noneの場合はリサイズしない
        height: 目標高さ（ピクセル）、Noneの場合はリサイズしない

    Returns:
        (画像バイナリ, 実際の幅, 実際の高さ)
    """
    try:
        img = Image.open(file_path)
        original_width, original_height = img.size

        # リサイズが必要かチェック
        if width is None and height is None:
            # リサイズ不要、オリジナルのまま
            target_width, target_height = original_width, original_height
        elif width is not None and height is not None:
            # 両方指定されている場合
            target_width, target_height = width, height
        elif width is not None:
            # 幅のみ指定、高さは縦横比を維持して計算
            aspect_ratio = original_height / original_width
            target_width = width
            target_height = int(width * aspect_ratio)
        else:
            # 高さのみ指定、幅は縦横比を維持して計算
            aspect_ratio = original_width / original_height
            target_height = height
            target_width = int(height * aspect_ratio)

        # リサイズ実行（必要な場合のみ）
        if (target_width, target_height) != (original_width, original_height):
            img = img.resize((target_width, target_height), Image.Resampling.LANCZOS)
            print(f"Image resized: {original_width}x{original_height} -> {target_width}x{target_height}")
        else:
            print(f"Image size: {original_width}x{original_height} (no resize)")

        # PNG形式でバイト列に変換
        from io import BytesIO
        buffer = BytesIO()

        # RGBA変換（透過対応）
        if img.mode in ('RGBA', 'LA'):
            img.save(buffer, format='PNG')
        elif img.mode == 'P':
            # パレットモードの場合もPNGで保存
            img.save(buffer, format='PNG')
        else:
            # RGB変換してPNGで保存
            if img.mode != 'RGB':
                img = img.convert('RGB')
            img.save(buffer, format='PNG')

        image_bytes = buffer.getvalue()
        return image_bytes, target_width, target_height

    except FileNotFoundError:
        print(f"Error: File not found: {file_path}")
        sys.exit(1)
    except Exception as e:
        print(f"Error: Failed to load or resize image: {e}")
        sys.exit(1)


def send_image_to_server(host: str, image_data: bytes, timeout_ms: int) -> Tuple[int, str]:
    """
    base64エンコードした画像をサーバーに送信します。

    Args:
        host: ホスト名:ポート (例: 192.168.1.73:34560)
        image_data: 画像バイナリデータ
        timeout_ms: タイムアウト時間（ミリ秒）

    Returns:
        (HTTPステータスコード, レスポンスボディ)
    """
    # base64エンコード
    base64_data = base64.b64encode(image_data).decode('ascii')

    # URLを構築
    url = f"http://{host}/image"

    # リクエストボディを準備（base64文字列 + CRLF×2で終端）
    request_body = base64_data + "\r\n\r\n"
    request_bytes = request_body.encode('utf-8')

    # HTTPリクエストを送信
    req = urllib.request.Request(url, data=request_bytes, method='POST')
    req.add_header('Content-Type', 'text/plain')

    timeout_sec = timeout_ms / 1000.0

    try:
        with urllib.request.urlopen(req, timeout=timeout_sec) as response:
            status_code = response.getcode()
            response_body = response.read().decode('utf-8')
            return status_code, response_body
    except urllib.error.HTTPError as e:
        # HTTPエラー（4xx, 5xx）
        error_body = e.read().decode('utf-8') if e.fp else ""
        return e.code, error_body
    except urllib.error.URLError as e:
        # ネットワークエラー、タイムアウト等
        return 0, str(e.reason)
    except Exception as e:
        return 0, str(e)


def main():
    parser = argparse.ArgumentParser(
        description='画像をサーバーに送信するツール',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python image_processor.py -host 192.168.1.73:34560 -file test_image.png
  python image_processor.py -host 192.168.1.73:34560 -file test.jpg -w 640
  python image_processor.py -host 192.168.1.73:34560 -file test.png -retry yes -retryMax 5 -retryInterval 1000
        """
    )

    parser.add_argument('-host', required=True, help='サーバーのホスト:ポート (例: 192.168.1.73:34560)')
    parser.add_argument('-file', required=True, help='送信する画像ファイルパス')
    parser.add_argument('-retry', default='no', choices=['yes', 'no'], help='リトライを有効にするか (default: no)')
    parser.add_argument('-retryMax', type=int, default=5, help='最大リトライ回数 (default: 5)')
    parser.add_argument('-retryInterval', type=int, default=1000, help='リトライ間隔（ミリ秒） (default: 1000)')
    parser.add_argument('-retryTimeout', type=int, default=5000, help='リクエストタイムアウト（ミリ秒） (default: 5000)')
    parser.add_argument('-w', type=int, help='画像の幅（ピクセル）')
    parser.add_argument('-h_size', type=int, dest='height', help='画像の高さ（ピクセル）※-hはヘルプと重複するため-h_sizeを使用')

    args = parser.parse_args()

    # ファイルの存在確認
    file_path = Path(args.file)
    if not file_path.exists():
        print(f"Error: File not found: {args.file}")
        print(f"Absolute path: {file_path.absolute()}")
        sys.exit(1)

    if not file_path.is_file():
        print(f"Error: Not a file: {args.file}")
        sys.exit(1)

    # 画像の読み込みとリサイズ
    print(f"Loading image: {args.file}")
    image_data, final_width, final_height = load_and_resize_image(
        str(file_path),
        width=args.w,
        height=args.height
    )

    print(f"Image data size: {len(image_data)} bytes")
    print(f"Final dimensions: {final_width}x{final_height}")

    # 送信処理
    retry_enabled = (args.retry == 'yes')
    max_attempts = args.retryMax if retry_enabled else 1
    retry_interval_sec = args.retryInterval / 1000.0

    attempt = 0
    while attempt < max_attempts:
        attempt += 1

        if attempt > 1:
            print(f"\nRetry attempt {attempt}/{max_attempts}...")
        else:
            print(f"\nSending image to http://{args.host}/image ...")

        status_code, response_body = send_image_to_server(
            args.host,
            image_data,
            args.retryTimeout
        )

        # 結果の表示
        if status_code == 200:
            print(f"Success! Status code: {status_code}")
            print(f"Response: {response_body}")

            # JSONレスポンスをパース（可能な場合）
            try:
                response_json = json.loads(response_body)
                if 'width' in response_json and 'height' in response_json:
                    print(f"Server reported display size: {response_json['width']}x{response_json['height']}")
                if 'display_ms' in response_json:
                    print(f"Display duration: {response_json['display_ms']}ms")
            except:
                pass

            sys.exit(0)

        elif status_code == 0:
            # ネットワークエラーやタイムアウト
            print(f"Error: Network error or timeout")
            print(f"Details: {response_body}")
        else:
            # HTTPエラー
            print(f"Error: HTTP status code {status_code}")
            print(f"Response: {response_body}")

        # リトライ判定
        if attempt < max_attempts:
            print(f"Waiting {args.retryInterval}ms before retry...")
            time.sleep(retry_interval_sec)
        else:
            print(f"\nFailed after {max_attempts} attempts.")
            sys.exit(1)


if __name__ == '__main__':
    main()
