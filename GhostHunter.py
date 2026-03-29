import os
import winreg
import difflib
import shutil
from pathlib import Path

WHITELIST = {
    "microsoft", "windows", "intel", "amd", "nvidia", 
    "realtek", "synaptics", "temp", "packages",
    "programs", "package cache", "comms", "crashdumps"
}

def get_installed_software() -> set:
    software_names = set()
    
    registry_paths = [
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
    ]
    
    for hkey, path in registry_paths:
        try:
            key = winreg.OpenKey(hkey, path, 0, winreg.KEY_READ)
            for i in range(0, winreg.QueryInfoKey(key)[0]):
                try:
                    subkey_name = winreg.EnumKey(key, i)
                    subkey = winreg.OpenKey(key, subkey_name)
                    
                    # Grab the App Name
                    display_name, _ = winreg.QueryValueEx(subkey, "DisplayName")
                    if display_name:
                        software_names.add(display_name.strip().lower())
                        
                    try:
                        publisher, _ = winreg.QueryValueEx(subkey, "Publisher")
                        if publisher:
                            # Strip out common corporate suffixes to improve fuzzy matching
                            pub_clean = publisher.lower().replace("inc.", "").replace("corp", "").replace("llc", "").strip()
                            software_names.add(pub_clean)
                    except OSError:
                        pass
                        
                except OSError:
                    pass 
        except OSError:
            pass 
            
    return software_names

def get_folder_size(folder_path: str) -> int:
    total_size = 0
    try:
        for dirpath, _, filenames in os.walk(folder_path):
            for f in filenames:
                fp = os.path.join(dirpath, f)
                if not os.path.islink(fp):
                    total_size += os.path.getsize(fp)
    except (OSError, PermissionError):
        pass
    return total_size

def find_ghost_folders() -> list:
    installed_software = get_installed_software()
    ghosts = []
    
    appdata_paths = [
        os.environ.get('LOCALAPPDATA'), 
        os.environ.get('APPDATA')       
    ]
    
    print(f"Tracking {len(installed_software)} installed programs & publishers...")
    print("Hunting for ghosts in AppData...\n")

    for appdata_dir in appdata_paths:
        if not appdata_dir or not os.path.exists(appdata_dir):
            continue
            
        try:
            with os.scandir(appdata_dir) as it:
                for entry in it:
                    if entry.is_dir(follow_symlinks=False):
                        folder_name = entry.name.lower()
                        
                        if folder_name in WHITELIST:
                            continue
                            
                        is_ghost = True 
                        
                        for app_name in installed_software:
                            if folder_name in app_name or app_name in folder_name:
                                is_ghost = False
                                break
                                
                            similarity = difflib.SequenceMatcher(None, folder_name, app_name).ratio()
                            if similarity > 0.65:
                                is_ghost = False
                                break
                        
                        if is_ghost:
                            size_bytes = get_folder_size(entry.path)
                            if size_bytes > 10_485_760: 
                                ghosts.append({
                                    "name": entry.name,
                                    "path": entry.path,
                                    "size_bytes": size_bytes
                                })
        except (PermissionError, OSError):
            pass

    ghosts.sort(key=lambda x: x["size_bytes"], reverse=True)
    return ghosts

def delete_ghost_folders(folder_paths: list) -> dict:
    """
    Safely deletes orphaned AppData folders.
    Includes a hardcoded security check to prevent deleting files outside AppData.
    """
    results = {"deleted": [], "failed": [], "freed_bytes": 0}
    
    appdata_local = os.environ.get('LOCALAPPDATA', '').lower()
    appdata_roaming = os.environ.get('APPDATA', '').lower()

    for path in folder_paths:
        try:
            if os.path.exists(path) and os.path.isdir(path):
                path_lower = path.lower()
                
                # ⚡ SECURITY GUARDRAIL: Only allow deletion if the path is strictly inside AppData
                if (appdata_local and path_lower.startswith(appdata_local)) or \
                   (appdata_roaming and path_lower.startswith(appdata_roaming)):
                    
                    # Calculate size before deleting for the UI metrics
                    size_bytes = get_folder_size(path)
                    
                    # Nuke the directory
                    shutil.rmtree(path)
                    
                    results["deleted"].append(path)
                    results["freed_bytes"] += size_bytes
                else:
                    results["failed"].append({"path": path, "error": "Security Block: Path is not inside AppData."})
            else:
                results["failed"].append({"path": path, "error": "Folder not found or is not a directory."})
        except Exception as e:
            results["failed"].append({"path": path, "error": str(e)})
            
    return results

if __name__ == "__main__":
    results = find_ghost_folders()
    for ghost in results[:10]: 
        size_mb = ghost['size_bytes'] / (1024 * 1024)
        print(f"GHOST FOUND: {ghost['name']} ({size_mb:.2f} MB)")
        print(f"  Path: {ghost['path']}")