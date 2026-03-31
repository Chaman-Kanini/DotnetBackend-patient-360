import sys
import json
import os
import pdfplumber
from docx import Document

# Maximum text length to prevent memory issues (10MB)
MAX_TEXT_LENGTH = 10 * 1024 * 1024

def is_text_empty_or_whitespace(text):
    """Check if extracted text is empty or contains only whitespace."""
    return not text or text.strip() == ""

def extract_text(file_path):
    """
    Extract text from PDF or DOCX files.
    
    Returns:
        dict: {"text": str, "error": str or None}
    """
    try:
        # Validate file exists
        if not os.path.exists(file_path):
            return {"text": "", "error": "File not found"}
        
        file_ext = file_path.lower()
        text = ""
        
        if file_ext.endswith('.pdf'):
            # Extract text from PDF using pdfplumber
            try:
                # Log file size for diagnostics
                file_size = os.path.getsize(file_path)
                sys.stderr.write(f"DEBUG: PDF file size: {file_size} bytes\n")
                
                with pdfplumber.open(file_path) as pdf:
                    sys.stderr.write(f"DEBUG: PDF has {len(pdf.pages)} pages\n")
                    sys.stderr.write(f"DEBUG: PDF metadata: {pdf.metadata}\n")
                    
                    for page_num, page in enumerate(pdf.pages, 1):
                        # Try multiple extraction strategies
                        # Strategy 1: Default extraction
                        page_text = page.extract_text()
                        
                        # Strategy 2: If default fails, try with layout parameters
                        if not page_text or page_text.strip() == "":
                            sys.stderr.write(f"DEBUG: Page {page_num}: Default extraction empty, trying with layout params\n")
                            page_text = page.extract_text(
                                layout=True,
                                x_tolerance=3,
                                y_tolerance=3
                            )
                        
                        # Strategy 3: If still empty, try extracting words directly
                        if not page_text or page_text.strip() == "":
                            sys.stderr.write(f"DEBUG: Page {page_num}: Layout extraction empty, trying word extraction\n")
                            words = page.extract_words()
                            if words:
                                page_text = " ".join([w["text"] for w in words])
                                sys.stderr.write(f"DEBUG: Page {page_num}: Extracted {len(words)} words\n")
                        
                        if page_text and page_text.strip():
                            text += page_text + "\n"
                            sys.stderr.write(f"DEBUG: Page {page_num}: Extracted {len(page_text)} characters\n")
                        else:
                            sys.stderr.write(f"DEBUG: Page {page_num}: No text extracted (may be image-only)\n")
                            # Check if page has images
                            if hasattr(page, 'images') and page.images:
                                sys.stderr.write(f"DEBUG: Page {page_num}: Contains {len(page.images)} images\n")
                
                sys.stderr.write(f"DEBUG: Total extracted text length: {len(text)} characters\n")
                sys.stderr.write(f"DEBUG: First 200 chars: {text[:200]}\n")
                
                # Check for scanned/image-only PDFs
                if is_text_empty_or_whitespace(text):
                    return {
                        "text": "",
                        "error": "No extractable text found. Document may be scanned or image-only."
                    }
            except Exception as e:
                sys.stderr.write(f"ERROR: PDF processing exception: {type(e).__name__}: {str(e)}\n")
                import traceback
                sys.stderr.write(f"ERROR: Traceback: {traceback.format_exc()}\n")
                return {
                    "text": "",
                    "error": f"PDF processing error: {str(e)}"
                }
        
        elif file_ext.endswith('.docx'):
            # Extract text from DOCX using python-docx
            doc = Document(file_path)
            text = "\n".join([para.text for para in doc.paragraphs])
            
            # Check for empty DOCX
            if is_text_empty_or_whitespace(text):
                return {
                    "text": "",
                    "error": "No extractable text found. Document appears to be empty."
                }
        
        else:
            return {
                "text": "",
                "error": f"Unsupported file format. Supported formats: .pdf, .docx"
            }
        
        # Truncate extremely large text to prevent memory issues
        if len(text) > MAX_TEXT_LENGTH:
            text = text[:MAX_TEXT_LENGTH]
            sys.stderr.write(f"Warning: Extracted text truncated to {MAX_TEXT_LENGTH} characters\n")
        
        return {"text": text, "error": None}
    
    except FileNotFoundError as e:
        error_msg = f"File not found: {str(e)}"
        sys.stderr.write(f"ERROR: {error_msg}\n")
        return {"text": "", "error": error_msg}
    
    except PermissionError as e:
        error_msg = f"Permission denied: {str(e)}"
        sys.stderr.write(f"ERROR: {error_msg}\n")
        return {"text": "", "error": error_msg}
    
    except Exception as e:
        error_msg = f"Failed to extract text: {type(e).__name__}"
        sys.stderr.write(f"ERROR: {error_msg} - {str(e)}\n")
        return {"text": "", "error": error_msg}

if __name__ == "__main__":
    # Validate command line arguments
    if len(sys.argv) < 2:
        error_result = {"text": "", "error": "No file path provided"}
        print(json.dumps(error_result))
        sys.stderr.write("ERROR: No file path provided\n")
        sys.exit(1)
    
    file_path = sys.argv[1]
    
    # Extract text
    result = extract_text(file_path)
    
    # Output JSON to stdout with ensure_ascii=False for proper Unicode handling
    try:
        json_output = json.dumps(result, ensure_ascii=False)
        sys.stdout.buffer.write(json_output.encode('utf-8'))
        sys.stdout.buffer.flush()
    except Exception as e:
        # Fallback to ASCII-only JSON if Unicode fails
        json_output = json.dumps(result, ensure_ascii=True)
        sys.stdout.buffer.write(json_output.encode('utf-8'))
        sys.stdout.buffer.flush()
    
    # Exit with appropriate code
    if result.get("error"):
        sys.exit(1)
    else:
        sys.exit(0)
