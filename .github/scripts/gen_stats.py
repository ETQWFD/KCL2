#!/usr/bin/env python3
"""汇总 KCL2 / KCL-CE / KCL-Mobile 三仓 GitHub Release 真实下载量，输出 website/_data/stats.json。
在 GitHub Actions 中运行，环境变量 GH_TOKEN 提供已鉴权令牌（不受匿名 60 次/小时限流）。"""
import json
import subprocess
import datetime
import os


def gh(url: str):
    out = subprocess.run(["gh", "api", url], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("gh api failed: " + url + "\n" + out.stderr)
    return json.loads(out.stdout)


def main():
    targets = [
        ("kcl2", "ETQWFD/KCL2", "tags/continuous"),
        ("ce", "ETQWFD/KCL-CE", "latest"),
        ("mobile", "ETQWFD/KCL-Mobile", "tags/continuous"),
    ]
    result = {
        "generated_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "repos": {},
    }
    for key, repo, mode in targets:
        latest = gh(f"repos/{repo}/releases/{mode}")
        releases = gh(f"repos/{repo}/releases?per_page=100")
        assets = [
            {
                "name": a["name"],
                "download_count": a["download_count"],
                "size": a.get("size", 0),
                "updated_at": a.get("updated_at"),
            }
            for a in latest.get("assets", [])
        ]
        latest_dl = sum(a["download_count"] for a in latest.get("assets", []))
        total_dl = sum(a["download_count"] for r in releases for a in r.get("assets", []))
        result["repos"][key] = {
            "repo": repo,
            "tag": latest.get("tag_name"),
            "name": latest.get("name"),
            "prerelease": latest.get("prerelease", False),
            "published_at": latest.get("published_at"),
            "html_url": latest.get("html_url"),
            "latest_downloads": latest_dl,
            "total_downloads": total_dl,
            "assets": assets,
        }

    os.makedirs("website/_data", exist_ok=True)
    with open("website/_data/stats.json", "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
    summary = {
        k: {"tag": v["tag"], "latest": v["latest_downloads"], "total": v["total_downloads"]}
        for k, v in result["repos"].items()
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
