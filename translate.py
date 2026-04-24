#!/usr/bin/env python3
"""
translate.py
======================
Completes the French translation of data/table.fr-FR.trans starting from a
given line number.  Lines that already contain French text are left untouched.

Supported translation backends
--------------------------------
1. DeepL Free/Pro API  (recommended)
     pip install deepl
     export DEEPL_API_KEY="your-key-here"

2. Google Cloud Translation v2 (Basic)
     pip install google-cloud-translate
     export GOOGLE_APPLICATION_CREDENTIALS="path/to/service-account.json"
     # OR: export GOOGLE_API_KEY="your-key-here"

3. Azure Translation
     pip install azure.ai.translation.text
     export AZURE_API_KEY="your-key-here"

Usage
-----
    # Translate everything from line 1 onwards (default)
    python translate.py

    # Start from a different line
    python translate.py --start 5000

    # Choose backend
    python translate.py --backend google

    # Dry-run: print first 20 lines that would be translated
    python translate.py --dry-run
"""

import argparse
import os
import re
import sys
import time

DATA_DIR = os.path.join(os.path.dirname(__file__), "data")
ORIG_FILE = os.path.join(DATA_DIR, "table.orig")
FR_FILE   = os.path.join(DATA_DIR, "table.fr-FR.trans")

# Regex to detect lines that are likely still English (not yet translated).
# Strategy: if the trans line == the orig line it hasn't been translated yet.
# We compare normalised (stripped) versions.

PLACEHOLDER_RE = re.compile(
    r"(\{[^}]+\}|<[^>]+>|\[[^\]]+\])"
)


def extract_text_segments(line: str):
    """Return (segments, template) where template has {SEG_N} markers and
    segments is the list of translatable substrings."""
    parts = PLACEHOLDER_RE.split(line)
    segments = []
    template_parts = []
    seg_idx = 0
    for part in parts:
        if PLACEHOLDER_RE.fullmatch(part):
            template_parts.append(part)
        else:
            if part:
                segments.append(part)
                template_parts.append(f"\x00SEG{seg_idx}\x00")
                seg_idx += 1
            # preserve empty strings between consecutive placeholders
    return segments, "".join(template_parts)


def rebuild(template: str, translated_segments) -> str:
    result = template
    for i, seg in enumerate(translated_segments):
        result = result.replace(f"\x00SEG{i}\x00", seg, 1)
    return result


# ---------------------------------------------------------------------------
# DeepL backend
# ---------------------------------------------------------------------------

def translate_batch_deepl(texts, api_key, target_lang="FR"):
    import deepl
    translator = deepl.Translator(api_key)
    results = translator.translate_text(texts, target_lang=target_lang)
    return [r.text for r in results]


# ---------------------------------------------------------------------------
# Azure Translator
# ---------------------------------------------------------------------------

# Initialize the Azure Translator client globally to reuse it across calls
from azure.ai.translation.text import TextTranslationClient
from azure.core.credentials import AzureKeyCredential
import time

_azure_client = None

def get_azure_client(api_key):
    global _azure_client
    if _azure_client is None:
        credential = AzureKeyCredential(api_key)
        _azure_client = TextTranslationClient(credential=credential, region="francecentral")
    return _azure_client

def translate_batch_azure(texts, api_key, target_lang="fr"):
    client = get_azure_client(api_key)

    response = client.translate(body=texts, to_language=[target_lang], from_language="en")

    translations = []
    for translation in response:
        if translation.translations:
            translations.append(translation.translations[0].text)
        else:
            translations.append("")  # Handle cases where no translation is returned

    # Wait for 10 seconds after each API call to handle throttling
    time.sleep(10)

    return translations


# ---------------------------------------------------------------------------
# Google Translate backend
# ---------------------------------------------------------------------------

def translate_batch_google(texts, api_key_or_creds, target_lang="fr"):
    from google.cloud import translate_v2 as translate
    if api_key_or_creds and not os.path.exists(api_key_or_creds):
        # plain API key
        client = translate.Client.from_service_account_info({})  # will fail
        # fall back to simple REST call
        import urllib.request, urllib.parse, json
        url = "https://translation.googleapis.com/language/translate/v2"
        payload = json.dumps({
            "q": texts,
            "target": target_lang,
            "format": "text",
            "key": api_key_or_creds,
        }).encode()
        req = urllib.request.Request(url, data=payload,
                                     headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req) as resp:
            data = json.loads(resp.read())
        return [t["translatedText"] for t in data["data"]["translations"]]
    else:
        client = translate.Client()
        result = client.translate(texts, target_language=target_lang)
        return [r["translatedText"] for r in result]


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Complete French translation.")
    parser.add_argument("--start", type=int, default=1,
                        help="First line number to translate (1-based, default 2701)")
    parser.add_argument("--backend", choices=["deepl", "google", "azure"], default="deepl")
    parser.add_argument("--batch-size", type=int, default=50,
                        help="Lines per API call (default 50)")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print first 20 lines that would be translated, then exit")
    args = parser.parse_args()

    # Load files
    with open(ORIG_FILE, "r", encoding="utf-8") as f:
        orig_lines = f.readlines()
    with open(FR_FILE, "r", encoding="utf-8") as f:
        fr_lines = f.readlines()

    total = len(orig_lines)
    assert len(fr_lines) == total, (
        f"Line count mismatch: orig={total}, fr={len(fr_lines)}"
    )

    start_idx = args.start - 1  # convert to 0-based

    # Collect indices that need translation
    todo = []
    for i in range(start_idx, total):
        orig = orig_lines[i].rstrip("\n")
        fr   = fr_lines[i].rstrip("\n")
        if orig.strip() == fr.strip() or fr.strip() == "":
            todo.append(i)

    print(f"Lines to translate: {len(todo)} (out of {total - start_idx} from line {args.start})")

    if args.dry_run:
        for i in todo[:20]:
            print(f"  [{i+1}] {orig_lines[i].rstrip()}")
        return

    # Select backend
    if args.backend == "deepl":
        api_key = os.environ.get("DEEPL_API_KEY", "")
        if not api_key:
            sys.exit("Set DEEPL_API_KEY environment variable.")
        def translate_batch(texts):
            return translate_batch_deepl(texts, api_key)
    else:
        if args.backend == "azure":
            api_key = os.environ.get("AZURE_API_KEY", "")
            if not api_key:
                sys.exit("Set AZURE_API_KEY environment variable.")
            def translate_batch(texts):
                return translate_batch_azure(texts, api_key)
        else:
            creds = os.environ.get("GOOGLE_API_KEY") or os.environ.get(
                "GOOGLE_APPLICATION_CREDENTIALS", "")
            if not creds:
                sys.exit("Set GOOGLE_API_KEY or GOOGLE_APPLICATION_CREDENTIALS.")
            def translate_batch(texts):
                return translate_batch_google(texts, creds)

    # Process in batches
    batch_size = args.batch_size
    updated = 0
    for batch_start in range(0, len(todo), batch_size):
        batch_indices = todo[batch_start: batch_start + batch_size]
        # Extract translatable segments per line
        all_segments = []
        line_meta = []   # (index_in_fr_lines, template, segment_count)
        for i in batch_indices:
            orig = orig_lines[i].rstrip("\n")
            segs, tmpl = extract_text_segments(orig)
            all_segments.extend(segs)
            line_meta.append((i, tmpl, len(segs)))

        if not all_segments:
            # All lines in this batch are pure placeholders — copy orig
            for i, tmpl, _ in line_meta:
                fr_lines[i] = orig_lines[i]
            continue

        # Translate all segments in one call
        try:
            translated = translate_batch(all_segments)
        except Exception as e:
            print(f"  API error at batch {batch_start}: {e}; skipping batch")
            time.sleep(2)
            continue

        # Rebuild lines
        seg_ptr = 0
        for i, tmpl, seg_count in line_meta:
            t_segs = translated[seg_ptr: seg_ptr + seg_count]
            seg_ptr += seg_count
            if seg_count == 0:
                fr_lines[i] = orig_lines[i]
            else:
                rebuilt = rebuild(tmpl, t_segs)
                # Preserve original trailing newline
                nl = "\n" if orig_lines[i].endswith("\n") else ""
                fr_lines[i] = rebuilt + nl
        updated += len(batch_indices)

        pct = (batch_start + len(batch_indices)) / len(todo) * 100
        print(f"  Translated {batch_start + len(batch_indices)}/{len(todo)} ({pct:.1f}%)")

        # Write checkpoint every 500 lines
        if updated % 500 == 0:
            with open(FR_FILE, "w", encoding="utf-8") as f:
                f.writelines(fr_lines)
            print(f"  Checkpoint saved ({updated} lines done).")

        time.sleep(0.1)  # be polite to the API

    # Final write
    with open(FR_FILE, "w", encoding="utf-8") as f:
        f.writelines(fr_lines)
    print(f"Done. {updated} lines updated. Final line count: {len(fr_lines)}")
    assert len(fr_lines) == total, "Line count changed — something went wrong!"


if __name__ == "__main__":
    main()
