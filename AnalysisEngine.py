import os
import time

def get_top_50_files(target_path: str) -> list:
    """Scans the target directory and returns the 50 absolute largest files."""
    all_files = []
    
    for root, _, files in os.walk(target_path):
        for file in files:
            filepath = os.path.join(root, file)
            try:
                if not os.path.islink(filepath):
                    size = os.path.getsize(filepath)
                    # We only care about files larger than 10MB to avoid clutter
                    if size > 10_485_760: 
                        all_files.append({
                            "path": filepath,
                            "size_bytes": size,
                            "name": file
                        })
            except (OSError, PermissionError):
                pass
                
    # Sort by size descending and slice the top 50
    all_files.sort(key=lambda x: x["size_bytes"], reverse=True)
    return all_files[:50]

def get_stale_files(target_path: str, days_old: int = 365, min_size_mb: int = 50) -> list:
    """
    Finds large files (>50MB) that haven't been modified or accessed in over a year.
    """
    stale_files = []
    current_time = time.time()
    seconds_in_days = days_old * 24 * 60 * 60
    min_bytes = min_size_mb * 1024 * 1024

    for root, _, files in os.walk(target_path):
        for file in files:
            filepath = os.path.join(root, file)
            try:
                if not os.path.islink(filepath):
                    stat = os.stat(filepath)
                    size = stat.st_size
                    
                    # We look at the Last Modified time
                    last_active = stat.st_mtime
                    
                    if size > min_bytes and (current_time - last_active) > seconds_in_days:
                        stale_files.append({
                            "path": filepath,
                            "size_bytes": size,
                            "name": file,
                            "last_active_timestamp": last_active
                        })
            except (OSError, PermissionError):
                pass
                
    # Sort by the largest wasted space first
    stale_files.sort(key=lambda x: x["size_bytes"], reverse=True)
    return stale_files[:50]