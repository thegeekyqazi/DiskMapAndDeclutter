import os
import shutil
import platform
from pathlib import Path

def get_temp_paths() -> list:
    """Gets the standard Windows Temp directories."""
    paths = [
        os.environ.get('TEMP'), # User Temp (C:\Users\<User>\AppData\Local\Temp)
        os.environ.get('TMP'),  # Fallback User Temp
        r"C:\Windows\Temp"      # System Temp (Requires Admin, but we can try)
    ]
    # Filter out None values, remove duplicates, and ensure the path actually exists
    return list(set(p for p in paths if p and os.path.exists(p)))

def scan_temp_space() -> dict:
    """
    Scans the Temp directories and returns the total amount of junk space.
    We don't return individual files here, just the aggregate math for the UI.
    """
    if platform.system() != "Windows":
        return {"total_files": 0, "total_bytes": 0, "paths_scanned": []}

    target_dirs = get_temp_paths()
    total_bytes = 0
    total_files = 0

    for temp_dir in target_dirs:
        try:
            # os.walk is fine here since we want to dive all the way to the bottom
            for dirpath, _, filenames in os.walk(temp_dir):
                for f in filenames:
                    fp = os.path.join(dirpath, f)
                    if not os.path.islink(fp):
                        try:
                            total_bytes += os.path.getsize(fp)
                            total_files += 1
                        except (OSError, PermissionError):
                            pass
        except (PermissionError, OSError):
            pass

    return {
        "total_files": total_files,
        "total_bytes": total_bytes,
        "paths_scanned": target_dirs
    }

def flush_temp_files() -> dict:
    """
    The aggressive sweeper. Attempts to delete everything inside the Temp folders.
    Gracefully skips files currently locked by the OS.
    """
    if platform.system() != "Windows":
        return {"freed_bytes": 0, "deleted_count": 0, "locked_count": 0}

    target_dirs = get_temp_paths()
    freed_bytes = 0
    deleted_count = 0
    locked_count = 0

    for temp_dir in target_dirs:
        try:
            with os.scandir(temp_dir) as it:
                for entry in it:
                    try:
                        if entry.is_file(follow_symlinks=False):
                            file_size = entry.stat().st_size
                            os.remove(entry.path)
                            freed_bytes += file_size
                            deleted_count += 1
                            
                        elif entry.is_dir(follow_symlinks=False):
                            # Get the size before we nuke the directory
                            dir_size = 0
                            for dirpath, _, filenames in os.walk(entry.path):
                                for f in filenames:
                                    fp = os.path.join(dirpath, f)
                                    if not os.path.islink(fp):
                                        try: dir_size += os.path.getsize(fp)
                                        except OSError: pass
                            
                            # shutil.rmtree recursively deletes the folder and all contents
                            shutil.rmtree(entry.path)
                            freed_bytes += dir_size
                            deleted_count += 1 # Counting the folder as 1 item for simplicity
                            
                    except (PermissionError, OSError):
                        # THIS IS EXPECTED. 
                        # Windows will lock files currently in use by running apps.
                        locked_count += 1
                        
        except (PermissionError, OSError):
            pass

    return {
        "freed_bytes": freed_bytes,
        "deleted_count": deleted_count,
        "locked_count": locked_count
    }

# --- Quick Terminal Test ---
if __name__ == "__main__":
    print("--- Scanning Temp Folders ---")
    scan_results = scan_temp_space()
    size_mb = scan_results['total_bytes'] / (1024 * 1024)
    print(f"Found {scan_results['total_files']} temp files wasting {size_mb:.2f} MB.")
    
    # UNCOMMENT TO ACTUALLY DELETE:
    # print("\n--- Flushing Temp Folders ---")
    # flush_results = flush_temp_files()
    # freed_mb = flush_results['freed_bytes'] / (1024 * 1024)
    # print(f"Successfully deleted {flush_results['deleted_count']} items.")
    # print(f"Freed up {freed_mb:.2f} MB!")
    # print(f"Skipped {flush_results['locked_count']} locked files currently in use.")
    