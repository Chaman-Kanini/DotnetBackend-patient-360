#!/usr/bin/env python3
import subprocess
import sys
import json

# Test the exact command that C# service runs
python_path = "python"
script_path = r"d:\flow\AI-Project\server\TrustFirstPlatform.API\bin\Debug\net10.0\extract_text.py"
file_path = r"d:\flow\AI-Project\server\TrustFirstPlatform.API\uploads\2026\01\14\2e368c24-4393-46a7-974e-48a0cff07a80\0a0b20b9edc841468e2d215172da352e.pdf"

try:
    # Run the process exactly like C# does
    result = subprocess.run(
        [python_path, script_path, file_path],
        capture_output=True,
        text=True,
        encoding='utf-8',
        timeout=60
    )
    
    print(f"Exit Code: {result.returncode}")
    print(f"Stdout length: {len(result.stdout)}")
    print(f"Stderr length: {len(result.stderr)}")
    
    if result.returncode == 0:
        try:
            data = json.loads(result.stdout)
            print("SUCCESS: JSON parsing successful")
            print(f"Text length: {len(data.get('text', ''))}")
            print(f"Error: {data.get('error')}")
            print(f"Success: {not data.get('error')}")
        except Exception as e:
            print("FAILED: JSON parsing failed:", str(e))
            print(f"Raw stdout: {repr(result.stdout[:200])}")
    else:
        print(f"Process failed with exit code {result.returncode}")
        print(f"Stderr: {result.stderr}")
        
except Exception as e:
    print(f"Process execution failed: {e}")
