import os
import platform
from pathlib import Path

# ⚡ VIRTUAL FILESYSTEM BLACKLIST
# These Linux directories map directly to Kernel/RAM. Scanning them will crash the app.
LINUX_BLACKLIST = {
    "/proc", "/sys", "/dev", "/run", "/snap", "/var/lib/lxcfs"
}

def build_storage_tree(root_path: str,ignore_folders: list = None) -> dict:
    target_dir = Path(root_path).resolve()
    current_os = platform.system()
    ignore_set = set(ignore_folders or [])


    if current_os == "Windows":
        if str(target_dir).lower().startswith(r"c:\windows"):
            raise PermissionError("Safety Guardrail: Scanning C:\\Windows is strictly prohibited.")

    def _scan(current_path: str) -> dict:
        node = {
            "name": os.path.basename(current_path) or current_path,
            "path": current_path,
            "size": 0,
            "children": []
        }

        #GUARDRAIL: Instantly abort if entering a virtual kernel directory
        if current_os == "Linux" and current_path in LINUX_BLACKLIST:
            return node

        try:
            with os.scandir(current_path) as it:
                for entry in it:
                    if entry.is_file(follow_symlinks=False):
                        try:
                            node["size"] += entry.stat(follow_symlinks=False).st_size
                        except (OSError, FileNotFoundError):
                            pass
                            
                    elif entry.is_dir(follow_symlinks=False):
                       
                        if entry.name in ignore_set:
                            continue
                            
                        child_node = _scan(entry.path)
                        node["size"] += child_node["size"]
                        
                        if child_node["size"] > 1_048_576:
                            node["children"].append(child_node)
                            
        except (PermissionError, OSError):
            pass

        return node
    
    return _scan(str(target_dir))

def flatten_for_plotly(tree_node: dict) -> dict:
    """Flattens the nested tree into parallel arrays for Plotly Sunburst."""
    ids, labels, parents, values = [], [], [], []

    def _traverse(node, parent_id=""):
        current_id = node["path"]
        
        ids.append(current_id)
        labels.append(node["name"])
        parents.append(parent_id)
        values.append(node["size"])

        if node["children"]:
            for child in node["children"]:
                _traverse(child, current_id)

    _traverse(tree_node)
    
    if parents:
        parents[0] = ""

    return {
        "ids": ids,
        "labels": labels,
        "parents": parents,
        "values": values
    }