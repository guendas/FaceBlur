import logging
import azure.functions as func
import os
import requests
import cv2
import numpy as np
import io

# Entry point
def main(myblob: func.InputStream):
    logging.info(f"Python blob trigger function processed blob \n"
                 f"Name: {myblob.name}\n"
                 f"Blob Size: {myblob.length} bytes")

    # Read image from Blob input
    image = myblob.read()
    
    # Get all faces in the image
    faces = get_face_rectangles(image)

    # For each face, blur it
    for face in faces:
        fr = face["faceRectangle"]
        origin = (fr["left"], fr["top"])
        image = blur_image(image,fr['height'],fr['width'],origin)

    # Write image on Blob output
    return image

# Use Face API to get face rectangles
def get_face_rectangles(image):
    # Face API Endpoint
    endpoint = f"https://{os.environ.get('cognitive_services_region')}.api.cognitive.microsoft.com/face/v1.0/detect"

    # Setting Headers parameters
    headers = {
        "Content-Type" : "application/octet-stream",
        "Ocp-Apim-Subscription-Key" : os.environ.get("cognitive_services_api_key")
    }
    #Setting query parameters
    params = {
        "detectionModel" : "detection_02"
    }
    # Call Face API passing the image, headers and parameters
    resp = requests.post(endpoint, headers= headers, params= params, data= image)
    # Return JSON with faces detected
    return resp.json()

# Blur image using OpenCV2
def blur_image(image, height, width, origin):
    # Read image if io.BytesIO type 
    if type(image) is io.BytesIO:
        image = image.read()

    # Decode image using numpy
    im = cv2.imdecode(np.frombuffer(image, np.uint8), cv2.IMREAD_COLOR)

    # Create ROI
    h,w = height,width
    x,y = origin[0],origin[1]
    roi = im[int(y):int(y)+int(h),int(x):int(x)+int(w)]

    # Blur image in ROI
    blurred_img = cv2.GaussianBlur(roi,(91,91),0)

    # Add blur to the overall img
    im[int(y):int(y)+int(h),int(x):int(x)+int(w)] = blurred_img

    # Return blurred img
    is_success, buffer = cv2.imencode(".jpg", im)
    io_buf = io.BytesIO(buffer)

    return io_buf