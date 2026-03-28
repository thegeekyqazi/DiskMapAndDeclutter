import os
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

# Import your custom logic from ScannerEngine.py
from ScannerEngine import build_storage_tree, flatten_for_plotly

app = FastAPI(title="Declutter Engine API")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], 
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/api/scan")
def scan_directory(target_path: str = "."):
   
    if not os.path.exists(target_path):
        raise HTTPException(status_code=404, detail="Directory not found")
    
    try:
        # Call the functions imported from ScannerEngine
        raw_tree = build_storage_tree(target_path)
        plotly_data = flatten_for_plotly(raw_tree)
        return plotly_data
    except PermissionError as e:
        raise HTTPException(status_code=403, detail=str(e))
    
