import os
import hashlib
import platform
from collections import defaultdict
from pathlib import Path

def find_duplicates(target_dir: str) -> list:
    target_path = Path(target_dir).resolve()

    current_os = platform.system()
    if current_os == "Windows":
        if str(target_path).lower().startswith(r"c:\windows"):
            raise PermissionError("Safety Guardrail: Scanning C:\\Windows is strictly prohibited.")
    elif current_os == "Linux":
        if str(target_path).startswith("/boot") or str(target_path) == "/":
            raise PermissionError("Safety Guardrail: Scanning root/boot is prohibited.")

  
    # STAGE 1: The Flat Crawler (Size Grouping)
  
    size_groups = defaultdict(list)
    
    def _flat_scan(current_path: str):
        try:
            with os.scandir(current_path) as it:
                for entry in it:
                    if entry.is_file(follow_symlinks=False):
                        try:
                            file_size = entry.stat(follow_symlinks=False).st_size
                            if file_size > 0: # Ignore 0-byte ghost files
                                size_groups[file_size].append(entry.path)
                        except (OSError, FileNotFoundError):
                            pass
                    elif entry.is_dir(follow_symlinks=False):
                        if entry.name not in ['.git', 'node_modules', '__pycache__']:
                            _flat_scan(entry.path)
        except (PermissionError, OSError):
            pass

    print("Running Stage 1: Grouping by exact byte size...")
    _flat_scan(str(target_path))
    
    # The Filter: Only keep sizes that have 2 or more files
    stage_1_survivors = list(paths for paths in size_groups.values() if len(paths) > 1)
    print(f"Stage 1 Complete: Found {len(stage_1_survivors)} size buckets with potential duplicates.")


    # STAGE 2: The 4KB Fast Hash (MD5)
    print("Running Stage 2: Hashing the first 4KB of survivors...")
    stage_2_survivors = []
    
    for paths_list in stage_1_survivors:
        fast_hash_groups = defaultdict(list)
        for path in paths_list:
            try:
                with open(path, 'rb') as f:
                    chunk = f.read(4096)
                    file_hash = hashlib.md5(chunk).hexdigest()
                    fast_hash_groups[file_hash].append(path)
            except (OSError, FileNotFoundError):
                pass
        
        for matched_paths in fast_hash_groups.values():
            if len(matched_paths) > 1:
                stage_2_survivors.append(matched_paths)
                
    print(f"Stage 2 Complete: Narrowed down to {len(stage_2_survivors)} highly likely buckets.")


    # STAGE 3: The Full Cryptographic Hash (SHA-256)
    print("Running Stage 3: Streaming full SHA-256 verification...")
    final_duplicates = []
    
    for paths_list in stage_2_survivors:
        full_hash_groups = defaultdict(list)
        for path in paths_list:
            try:
                hasher = hashlib.sha256()
                with open(path, 'rb') as f:
                    # Stream the file in 64KB chunks to prevent RAM overflow on massive files
                    while chunk := f.read(65536):
                        hasher.update(chunk)
                full_hash_groups[hasher.hexdigest()].append(path)
            except (OSError, FileNotFoundError):
                pass
                
        for hash_val, matched_paths in full_hash_groups.items():
            if len(matched_paths) > 1:
                final_duplicates.append({
                    "hash": hash_val,
                    "size_bytes": os.path.getsize(matched_paths[0]),
                    "files": matched_paths
                })
    print(f"Stage 3 Complete: Verified {len(final_duplicates)} sets of exact duplicates.")
    return final_duplicates


def delete_selected_files(file_paths: list) -> dict:
    """
    Takes a specific list of file paths from the frontend and deletes them.
    Returns a summary of successes and failures.
    """
    results = {"deleted": [], "failed": []}
    
    for file_path in file_paths:
        try:
            # Re-verify the file actually exists before trying to delete
            if os.path.exists(file_path):
                os.remove(file_path)
                results["deleted"].append(file_path)
            else:
                results["failed"].append({"path": file_path, "error": "File not found"})
        except OSError as e:
            results["failed"].append({"path": file_path, "error": str(e)})
            
    return results




# --- Quick Terminal Test ---
if __name__ == "__main__":
    # Test this by running `python DeduplicatorEngine.py`
    results = find_duplicates("/home/geeki/stuff/coding")
    
    print("\n--- JSON PAYLOAD PREVIEW ---")
    for group in results[:2]: # Just print the first two groups so it doesn't flood the terminal
        print(f"Size: {group['size_bytes']} bytes")
        for f in group['files']:
            print(f"  - {f}")