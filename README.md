# Setting up Adobe PDF Embed API token

1. Go to [Adobe PDF Embed API Integration](https://acrobatservices.adobe.com/dc-integration-creation-app-cdn/main.html?api=pdf-embed-api).
2. Register or log in to your account.
3. Enter your credentials, domain of your app, agree to developer terms, and press “Create credentials”.
4. Copy the CLIENT ID (API KEY) that you received and paste it into `appsettings` under the key `AdobeAPIToken`.

# Installation

## Prerequisites

- Visual Studio
- [.NET Hosting Bundle](https://dotnet.microsoft.com/permalink/dotnetcore-current-windows-runtime-bundle-installer)

## Steps

1. Clone the repository.
2. Open the repository with your preferred IDE (For guide purposes, I will use Visual Studio).
3. Go to `appsettings.json` and fill in the path where notes should be stored on the server (`NotesFolder`) and paste your Adobe token into `AdobeAPIToken`.
4. Publish the solution. (For detailed instructions, see "Publishing with Visual Studio" below)
5. Open up IIS.
6. Expand connections list, right click on "Sites" and press "Add website".
7. Choose a name for the website, ex. "SharingAPI". Then, for physical path, pick the location where you published the project to. In the Binding section, choose whatever you want to host it under. Then, press OK.

## Publishing with Visual Studio

1. Right click on the project solution and press Publish.
2. In the Publish window, press "Add a publish profile".
3. Select "Folder" and press next.
4. Pick the location where you want to publish the solution.
5. Press "Finish", and then "Close".
6. Now, press "Publish" next to the generated publish profile.

## Adding CORS Headers to IIS Web.config

To enable CORS (Cross-Origin Resource Sharing) on an IIS server, you can add CORS headers to the web.config file of your website or application. This allows the server to specify which origins are permitted to access its resources.

### Prerequisites

- IIS CORS Module installed: [Instalation link](https://www.iis.net/downloads/microsoft/iis-cors-module)

### Steps

1. **Access web.config**: Locate and access the `web.config` file for your website or application. This file is typically located in the root directory of your application.

2. **Edit web.config**: Open the `web.config` file using a text editor.

3. **Add CORS Configuration**: Inside the `<system.webServer>` section of the `web.config` file, add the following XML configuration to enable CORS and specify the desired CORS headers. Example:

    ```xml
    <?xml version="1.0" encoding="utf-8"?>
    <configuration>
    <location path="." inheritInChildApplications="false">
        <system.webServer>
            <cors enabled="true" failUnlistedOrigins="true">
                <add origin="*">
                <allowMethods>
                    <add method="GET" />
                    <add method="POST" />
                    <add method="PUT" />
                    <add method="DELETE" />
                </allowMethods>
                </add>
            </cors>
        </system.webServer>
    </location>
    </configuration>
    ```

    In the above example:
    - `enabled="true"` enables CORS support.
    - `failUnlistedOrigins="true"` specifies that requests from origins not listed in the configuration should fail.
    - `<add origin="*">` allows requests from any origin.
    - `<allowMethods>` specifies the allowed HTTP methods (GET, POST, PUT, DELETE).
    - '*' `<allowHeaders>` specifies the allowed request headers.
    - '*' `<exposeHeaders>` specifies the headers that the server exposes to the client.

    <sub><sup>'*' - optional tags</sup></sub>

4. **Save Changes**: After adding the CORS configuration to the `web.config` file, save the changes.

5. **Restart IIS**: To apply the changes, restart the IIS server. You can do this by selecting the server node in the IIS Manager and clicking "Restart" under the Manage Server section.

Once these steps are completed, the IIS server will include the specified CORS headers in its responses, allowing cross-origin requests for the specified HTTP methods.

## Sample Publish

In the releases, there is an already published current version of the API with sample notes folder set up. 
Unpack it into your site's location and modify `appsettings.json` and `web.config` according to your needs.

API server should now be correctly set up.