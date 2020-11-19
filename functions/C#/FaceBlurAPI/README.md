# Face Blur API
This C# Azure function allow you to recognize one or more faces on an image and blur them. It leverages Azure Cognitive Services Face API and OpenCV library.

It accepts one parameter (*url*) in query string with the http url of the image to blur. The result is a JSON message that contains the original url of the image, the blurred one url and a result message in case of any errors. The blur url is an url to the generated image on a blob account container (see later) with SAS token that make the url valid for a specific amount of time (configured). After that time you have to acces to the private container in your blob storage to look at the image processed.

Sample return JSON:

```
{
    "urlOriginalImg": "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-sample-data-files/master/ComputerVision/Images/faces.jpg",
    "urlBlurredSASImg": "https://faceblurred.blob.core.windows.net/api/19/11/2020/08/38/35/blurred/faces.jpg?sv=2020-02-10&st=2020-11-19T08%3A38%3A36Z&se=2020-11-19T09%3A08%3A36Z&sr=b&sp=r&sig=R0SrFxeI3iClV7Io8Sljlgd5mcLcMQHTnQtxUoKl9Nw%3D",
    "resMsg": "OK"
}
```

## Run the function locally
You can run the Azure function locally creating a ```local.settings.json```, there is a sample one in the documentation, and configuring the following parameters:

- SUBSCRIPTION_KEY: "your Cognitive Service API Key"
- ENDPOINT: "https://your-endpoint.cognitiveservices.azure.com"
- STORAGE_CONNECTIONSTRING: the connection string to the storage account that contain the blurred images.
- CONTAINER_NAME_LOWER_CASE: the name of the root container that will contain blurred images. The created images will have a date/time/ path format.
- SAS_TOKEN_MINUTES_VALIDITY: The duration of the returned url to the blurred image to be valid (in minutes).

## Run the function in Azure
You can run the the C# function in Azure both as 32 bit and 64 bit, not any CPU. You can use both consumption and the standard web app model.

From Visual Studio, right click on the project and press Publish.
Now press Azure and Next as in the following image
![Fun1](FaceBlurAPI/Docs/fun1.png)

Now Select Azure Function App (Windows)
![Fun2](FaceBlurAPI/Docs/fun2.png)

You can use an already created Function or create a new one, pressing the '+' sign
![Fun3](FaceBlurAPI/Docs/fun3.png)

Here you have to fill the form choosing a Resource Group, Location, Plan type and an Azure Storage. You can create an Azure Storage account if you need it.
![Fun4](FaceBlurAPI/Docs/fun4.png)

After some minutes you will see a summary view and press Finish.
![Fun5](FaceBlurAPI/Docs/fun5.png)

Before publish the function, you have to configure if you want to run on 32 or 64 bit platform. Press edit on the profile created
![Fun6](FaceBlurAPI/Docs/fun6.png)

In profile setting, change configuraion and runtime target as below for 32 bit (change to 64 bit in the other case)
![Fun7](FaceBlurAPI/Docs/fun7.png).

Now you are able to test the function, if you open it into the portal, select "Functions" and then the your function name, you will be able to run some code test.
Create a new parameter url and make it point to a test image.
![Fun8](FaceBlurAPI/Docs/fun8.png)

As from image above, on your first run you will get some error messages, because you haven't configured the parameters as for the local one.

Go in the configuration section and add the 5 parameters as discussed above and press save
![Fun9](FaceBlurAPI/Docs/fun9.png)


Now it should work as expected

![Fun10](FaceBlurAPI/Docs/fun10.png)

Following the urlBlurredImage field you will be able to look at the resulting image until the SAS token appended to url is valid.
Now if you run again the function, it will work properly as in the image below.
![Faces](FaceBlurAPI/Docs/faces.jpg)

 