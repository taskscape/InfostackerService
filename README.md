# Setting up Adobe PDF Embed API token:

1. Go to [Adobe PDF Embed API Integration](https://acrobatservices.adobe.com/dc-integration-creation-app-cdn/main.html?api=pdf-embed-api).
2. Register or log in to your account.
3. Enter your credentials, domain of your app, agree to developer terms, and press “Create credentials”.
4. Copy the CLIENT ID (API KEY) that you received and paste it into `appsettings` under the key `AdobeAPIToken`.

# Installation:

## Prerequisites:
- Visual Studio
- [.NET Hosting Bundle](https://dotnet.microsoft.com/permalink/dotnetcore-current-windows-runtime-bundle-installer)

## Steps:
1. Clone the repository.
2. Open the repository with your preferred IDE (For guide purposes, I will use Visual Studio).
3. Go to `appsettings.json` and fill in the path where notes should be stored on the server (`NotesFolder`) and paste your Adobe token into `AdobeAPIToken`.
4. Publish the solution. (For detailed instructions, see "Publishing with Visual Studio" below)
5. Open up IIS.
6. Expand connections list, right click on "Sites" and press "Add website".
7. Choose a name for the website, ex. "SharingAPI". Then, for physical path, pick the location where you published the project to. In the Binding section, choose whatever you want to host it under. Then, press OK.

## Publishing with Visual Studio:
1. Right click on the project solution and press Publish.
2. In the Publish window, press "Add a publish profile".
3. Select "Folder" and press next.
4. Pick the location where you want to publish the solution.
5. Press "Finish", and then "Close".
6. Now, press "Publish" next to the generated publish profile.

API server should now be correctly set up.