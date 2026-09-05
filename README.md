<img width="1280" height="640" alt="git (1)" src="https://github.com/user-attachments/assets/8920b256-2ba8-4988-b824-5351134eb4bd" />



# Allik Aabharanam 


## Basic Details
### Team Name: EvaNora


### Team Members
- Team Lead: Rohana - Jain (deemed-to-be) University
- Member 2:  Anjali - Jain (deemed-to-be) University

### Project Description
"Allik Aabharanam" is a Windows desktop application that visually attaches decorative keychains and accessories to real files, folders, and shortcuts on your Windows desktop. As you drag your icons across the desktop, the accessories smoothly follow them in real time without modifying the files in any way.

### The Problem (that doesn't exist)
Real files and folders all behave and look essentially the same, so there is little visual or interactive connection between the user and their desktop environment. At the same time, simply adding random animations or sounds can make a desktop companion distracting and annoying rather than useful or enjoyable.

### The Solution (that nobody asked for)
Allikk Aabharanam is a playful desktop companion that adds unnecessary personality to ordinary files and folders. Instead of changing the actual desktop icons, our system attaches decorative accessories to them based on their position and behavior. It tracks the icons using their coordinates, calculates where an accessory should sit relative to the icon, and renders it as a separate layer. We also introduced a rule-based audio system, so the companion can react to certain desktop interactions with sounds. Accessories can be assigned manually or automatically from our dataset. So essentially, we took something that absolutely does not need to be decorated — a desktop file — and built an entire system to decorate it properly.

## Technical Details
### Technologies/Components Used
For Software:
- Languages: C#
- Frameworks: .NET 8 / WPF
- Libraries: NAudio (audio playback), System.Drawing (image/bitmap processing), and Win32/Windows Shell APIs through our C# interop code.
- Tools: VS Code, .NET CLI, Git, GitHub, Antigravity

For Hardware:
- Laptop/PC
- No specific hardware required


### Implementation
For Software:
# Installation

Make sure .NET 8 SDK is installed on the Windows system.
Clone/download the project and open the project folder in VS Code or Visual Studio.
Restore the required NuGet dependencies:

# Run
dotnet restore
#Build and run the application:
dotnet build
dotnet run

### Project Documentation
For Software:

# Screenshots (Add at least 3)
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/3ac8e3a1-2a43-4459-9c9f-fc655a57eac3" />
(screenshot 1)
*User Interface*

<img width="927" height="687" alt="image" src="https://github.com/user-attachments/assets/4414428d-1aa4-4bb0-a438-94ec7b0a3336" />
(screenshot 2)
*Accessories with manual and automatic settings*

<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/22915762-50c8-4d29-a66d-99b4d37854ff" />
(screenshot 3)
*Implementation of the accessories in the desktop icons*

# Diagrams
                    ┌─────────────────────┐
                    │   Windows Desktop   │
                    │  Files & Folders    │
                    └──────────┬──────────┘
                               ↓
                    ┌─────────────────────┐
                    │  User selects icon  │
                    └──────────┬──────────┘
                               ↓
                    ┌─────────────────────┐
                    │   KeychainTracker   │
                    │                     │
                    │ Finds icon position │
                    │ & tracks movement   │
                    └──────────┬──────────┘
                               ↓
                    ┌─────────────────────┐
                    │  Icon Coordinates   │
                    │    & Geometry       │
                    └──────────┬──────────┘
                               ↓
                    ┌─────────────────────┐
                    │ Accessory Provider  │
                    │ / Assignment System │
                    └──────────┬──────────┘
                               ↓
                    ┌─────────────────────┐
                    │ Placement Geometry  │
                    │                     │
                    │ Calculates position │
                    │ relative to icon    │
                    └──────────┬──────────┘
                               ↓
              ┌────────────────┴────────────────┐
              ↓                                 ↓
     ┌─────────────────┐              ┌─────────────────┐
     │ Accessory Asset │              │  Desktop Icon   │
     │   from Dataset  │              │  stays original │
     └────────┬────────┘              └────────┬────────┘
              │                                │
              └──────────────┬─────────────────┘
                             ↓
                  ┌─────────────────────┐
                  │ Accessory Renderer  │
                  │                     │
                  │ Renders accessory   │
                  │ as separate layer   │
                  └──────────┬──────────┘
                             ↓
                  ┌─────────────────────┐
                  │ Transparent Overlay │
                  │                     │
                  │ Accessory + thin    │
                  │ protective layer    │
                  └──────────┬──────────┘
                             ↓
                  ┌─────────────────────┐
                  │   Audio Manager     │
                  │                     │
                  │ Contextual sounds   │
                  │ for interactions    │
                  └──────────┬──────────┘
                             ↓
                  ┌─────────────────────┐
                  │ Interactive Desktop │
                  │     Companion       │
                  └─────────────────────┘
                  
***Caption:**
*Workflow of the desktop companion showing how the system tracks the selected icon, calculates its position, assigns and places an accessory, renders it as a separate overlay, and adds interactive audio without modifying the original desktop icon.*
*

For Hardware:

# Schematic & Circuit
![Circuit](Add your circuit diagram here)
*Add caption explaining connections*

![Schematic](Add your schematic diagram here)
*Add caption explaining the schematic*

# Build Photos
<img width="1552" height="502" alt="image" src="https://github.com/user-attachments/assets/6f64862e-03ff-469c-bd11-57089c68ba9a" />


<img width="817" height="387" alt="image" src="https://github.com/user-attachments/assets/a247ed4e-c0f4-49a4-b5ff-1e6c7ebbd0c5" />


<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/b3893b7c-f7bf-4557-a157-3c939ad1922e" />


### Project Demo
# Video
https://drive.google.com/file/d/1cwROpGTg5UkpMOzBEhD1Z_oSfw8sJPKv/view?usp=drive_link
*This demonstrates the application-level implementation of “Alikk Aabharanam,” showing the accessories attached to desktop files, their movement with the icons, and the accompanying sounds during interactions.*

# Additional Demos
None

## Team Contributions
* **[Rohana]:** Desktop icon identification and tracking, architecture and workflow design of Allik Aabharanam, and accessory design and integration.
* **[Anjali]:** Application development, audio integration and implementation, and GitHub/repository management.



---
Made with ❤️ at TinkerHub Useless Projects 

![Static Badge](https://img.shields.io/badge/TinkerHub-24?color=%23000000&link=https%3A%2F%2Fwww.tinkerhub.org%2F)
![Static Badge](https://img.shields.io/badge/UselessProjects--26-26?link=https%3A%2F%2Ftinkerhub.org%2Fevents%2F1M8ORET9A1%2Fuseless-projects-3.0)



