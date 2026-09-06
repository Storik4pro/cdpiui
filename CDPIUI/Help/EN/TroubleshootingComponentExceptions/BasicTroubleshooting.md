# Troubleshooting component exceptions

### Before You Begin
First, check the error code. It is usually displayed at the top of the [Pseudo Console] window in the "This component has terminated with an error: \<Exception Code\>" menu.

|Exception Code  | Description                                  | Possible Fixes                 |
|----------------|----------------------------------------------|--------------------------------|
|PARAMETER_ERROR | The flag passed to the component is not supported | Update the component being used to the latest version.<br>Make sure a preset is selected for the component and that it is not empty.<br>If you created a preset manually, please review the component's official documentation. Some of the specified flags may not be supported on your platform.|
|FILTER_OPEN_ERROR| Failed to start the WinDivert filter. | Reinstall the component<br>Make sure the component preset contains the correct port information<br>Make sure nothing is blocking traffic management on your PC. |
|HOSTLIST_LOAD_ERROR<br>FILE_READ_ERROR| Failed to load site list| Reinstall the preset set<br>Make sure all site lists/IP addresses used in the preset exist in the preset folder |
|PORT_FILTER_ERROR| WinDivert could not determine the port to run on| Make sure the component preset contains the correct port information|
|COMPONENT_INSTALL_ERROR| The component was installed incorrectly | Reinstall the component|
|INVALID_VALUE_ERROR| The flag value passed is invalid| Update the component being used to the latest version<br>If you created the preset manually, please review the component's official documentation. Some of the specified flags and their values ​​may not be supported on your platform.|
|ALREADY_RUNNING_WARN| An instance of WinDivert is already running on this PC.| Stop the WinDivert service.<br>Remove or disable all existing bypass tools except this application.|
|ACCESS_DENIED | Unable to access file.|Verify that the application, component executable, and WinDivert service have read and write permissions in the installation directory.|