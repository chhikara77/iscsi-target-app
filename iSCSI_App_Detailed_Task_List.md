# Detailed Task List: iSCSI Windows Application Implementation

This document provides a step-by-step guide for an AI Coder to implement the iSCSI Windows Application. Refer to the `iSCSI_Windows_App_Planning_Document.md` for overall architecture and concepts.

## Phase 0: Environment Setup & Project Initialization

1.  **Setup Development Environment:**
    *   Install Visual Studio (latest version recommended) with .NET desktop development workload (for C# and WPF/UWP) and C++ desktop development workload (if C++ components are planned).
    *   Ensure .NET Framework (e.g., 4.8) or .NET (e.g., .NET 6+) is installed.
2.  **Create Solution and Projects:**
    *   Create a new Visual Studio Solution (e.g., `WindowsIscsiTarget`).
    *   **Core Logic Project (Windows Service):**
        *   Create a C# Class Library project (e.g., `IscsiTarget.Core`). This will later be hosted in a Windows Service.
        *   Alternatively, create a C# Windows Service project directly.
    *   **User Interface Project:**
        *   Create a C# WPF App or UWP App project (e.g., `IscsiTarget.UI`).
    *   **(Optional) Shared Library Project:**
        *   Create a C# Class Library project for shared data structures and utilities (e.g., `IscsiTarget.Shared`).
3.  **Install Necessary NuGet Packages (Core Logic):**
    *   Consider logging libraries (e.g., Serilog, NLog).
    *   No specific iSCSI libraries are assumed; the protocol will be implemented from scratch based on RFC 7143.

## Phase 1: Core Protocol Implementation (in `IscsiTarget.Core`)

*Goal: Implement basic iSCSI listener, session establishment, and handling of essential PDUs for login and negotiation.*

1.  **Define iSCSI PDU Structures:**
    *   Create C# classes/structs to represent iSCSI PDUs (Protocol Data Units) as per RFC 7143 (e.g., `LoginRequestPDU`, `LoginResponsePDU`, `TextRequestPDU`, `TextResponsePDU`, `ScsiCommandPDU`, `ScsiResponsePDU`, `NopOutPDU`, `NopInPDU`).
    *   Include methods for serializing to and deserializing from byte arrays.
    *   Pay close attention to byte order (Big-Endian/Network Byte Order).
2.  **Implement TCP Listener (`IscsiTargetServer.cs`):**
    *   Create a class `IscsiTargetServer`.
    *   Method `Start(IPAddress address, int port)`: Initializes a `TcpListener` to listen on the specified IP and port (default iSCSI port is 3260).
    *   Asynchronously accept incoming `TcpClient` connections.
    *   For each connection, create an `IscsiSession` object and handle it on a new thread or using async/await patterns.
3.  **Implement Session Management (`IscsiSession.cs`):**
    *   Create a class `IscsiSession` to manage a single initiator connection.
    *   **Login Phase:**
        *   Handle `LoginRequestPDU`.
        *   Implement negotiation of parameters (e.g., `MaxRecvDataSegmentLength`, `HeaderDigest`, `DataDigest`). Store negotiated parameters.
        *   Send `LoginResponsePDU`.
        *   Transition to Full Feature Phase upon successful login.
    *   **Full Feature Phase:**
        *   Loop to receive and process PDUs.
        *   Handle `NopOutPDU` (send `NopInPDU`).
        *   Handle `LogoutRequestPDU` (send `LogoutResponsePDU` and close session).
        *   Placeholder for `ScsiCommandPDU` (to be implemented in Phase 2).
    *   Implement PDU sending and receiving logic (handle `NetworkStream`, potential digests, data segmentation if `MaxRecvDataSegmentLength` is exceeded by initiator).
4.  **Configuration (`TargetConfiguration.cs`):**
    *   Create a class `TargetConfiguration` to hold target-wide settings (e.g., TargetName IQN, listening IP/port).
    *   Implement loading/saving this configuration (e.g., from an XML or JSON file).
    *   The `IscsiTargetServer` should use this configuration.

## Phase 2: Storage Abstraction and Basic LUN Management (in `IscsiTarget.Core`)

*Goal: Abstract storage backend, implement LUNs using VHDX files, and handle basic SCSI commands (Inquiry, Test Unit Ready, Read, Write).*

1.  **Storage Abstraction Layer:**
    *   Define an interface `IStorageBackend` with methods like `Read(long offset, int length)`, `Write(long offset, byte[] data)`, `GetCapacity()`, `InquiryData()`, `TestUnitReady()`. 
    *   Implement `VhdxStorageBackend.cs` that implements `IStorageBackend`:
        *   Constructor takes a VHDX file path.
        *   Uses a library or custom code to read/write to the VHDX file (e.g., `System.IO.FileStream` for raw access if VHDX is simple fixed size, or a more complex VHDX parsing library if dynamic VHDX features are needed. For simplicity, start with fixed-size VHDX or raw image files).
        *   Implement `InquiryData()` to return standard inquiry data (Device Type 0x00 for Direct Access Block Device).
        *   Implement `TestUnitReady()` (typically returns success if LUN is accessible).
2.  **LUN Management (`LunManager.cs` and `Lun.cs`):**
    *   Create a class `Lun` representing a Logical Unit.
        *   Properties: `LunId` (byte), `StorageBackend` (`IStorageBackend`), `Name`.
    *   Create a class `LunManager`.
        *   Manages a collection of `Lun` objects.
        *   Methods: `AddLun(string vhdxPath, byte lunId)`, `RemoveLun(byte lunId)`, `GetLun(byte lunId)`.
        *   Persist LUN configurations (e.g., in the target configuration file).
3.  **SCSI Command Processing (in `IscsiSession.cs`):**
    *   Modify the Full Feature Phase PDU loop to handle `ScsiCommandPDU`.
    *   Extract the LUN from the CDB (Command Descriptor Block).
    *   Retrieve the corresponding `Lun` object from `LunManager`.
    *   **Implement Handlers for SCSI Commands:**
        *   `INQUIRY (0x12)`: Use `IStorageBackend.InquiryData()`.
        *   `TEST UNIT READY (0x00)`: Use `IStorageBackend.TestUnitReady()`.
        *   `READ(10) (0x28)` / `READ(16) (0x88)`: Extract LBA and length, call `IStorageBackend.Read()`. Send data back in `DataInPDU` (if solicited) followed by `ScsiResponsePDU`.
        *   `WRITE(10) (0x2A)` / `WRITE(16) (0x8A)`: Expect `DataOutPDU`(s), extract LBA and length, call `IStorageBackend.Write()`. Send `ScsiResponsePDU`.
        *   `REPORT LUNS (0xA0)`: Return a list of configured LUNs.
        *   `READ CAPACITY(10) (0x25)` / `READ CAPACITY(16) (0x9E)`: Use `IStorageBackend.GetCapacity()`.
    *   Construct and send `ScsiResponsePDU` with appropriate status (GOOD, CHECK CONDITION, etc.) and sense data if needed.
4.  **Initial Testing:**
    *   Use Windows iSCSI Initiator to connect to the target.
    *   Verify LUNs are discovered and can be formatted and used for basic file operations.

## Phase 3: UI Development and Service Integration

*Goal: Create a UI for managing the target and LUNs, and integrate the core logic into a Windows Service.*

1.  **Windows Service (`IscsiTargetService.cs` in a new Windows Service Project):**
    *   Create a new C# Windows Service project (e.g., `IscsiTarget.Service`).
    *   Reference `IscsiTarget.Core`.
    *   In `OnStart()`: Instantiate `IscsiTargetServer` and `LunManager`, load configuration, and start the server.
    *   In `OnStop()`: Stop the `IscsiTargetServer` and release resources.
    *   Implement service installation logic (e.g., using `InstallUtil.exe` or a setup project).
2.  **IPC Mechanism (e.g., Named Pipes):**
    *   Define contracts for communication between UI and Service (e.g., commands to get target status, list LUNs, add/remove LUN, get stats).
    *   Implement Named Pipe server in the `IscsiTarget.Service` (or within `IscsiTarget.Core` if it's to be managed there).
    *   Implement Named Pipe client in the `IscsiTarget.UI`.
3.  **User Interface (`IscsiTarget.UI` - WPF/UWP):**
    *   **Main Window:**
        *   Display target status (running/stopped, listening IP/port).
        *   Buttons to Start/Stop the iSCSI service (requires admin privileges for service control).
    *   **LUN Management View:**
        *   List current LUNs (ID, VHDX path, size, status).
        *   Button to "Add LUN" (prompts for VHDX file path and desired LUN ID).
        *   Button to "Remove LUN".
    *   **Settings View:**
        *   Configure Target Name (IQN).
        *   Configure listening IP address and port.
    *   **Log Viewer (Basic):**
        *   Display logs from the service (requires IPC for log messages or reading from a shared log file).
    *   Implement ViewModels and bind them to UI elements.
    *   Use the IPC client to communicate with the service for all operations.

## Phase 4: Advanced Features and Security

*Goal: Implement CHAP authentication, LUN masking, and improve robustness.*

1.  **CHAP Authentication (in `IscsiSession.cs` and `TargetConfiguration.cs`):**
    *   Extend `TargetConfiguration` to store CHAP credentials (username, secret) for the target and for authenticating initiators.
    *   Modify the iSCSI Login Phase in `IscsiSession.cs`:
        *   Negotiate `AuthMethod` (e.g., `CHAP`).
        *   Implement CHAP algorithm (RFC 1994).
        *   Handle CHAP PDUs (`LoginRequestPDU` with CHAP A, C, N, R, I, K fields; `LoginResponsePDU` with CHAP fields).
        *   Allow UI to configure CHAP credentials.
2.  **LUN Masking (Initiator Groups / Access Control):**
    *   Extend `TargetConfiguration` and `LunManager` to associate LUNs with specific Initiator IQNs.
    *   During `REPORT LUNS` and SCSI command processing, check if the connected initiator's IQN is authorized for the requested LUN.
    *   Allow UI to configure these mappings.
3.  **Error Handling and Logging:**
    *   Implement robust error handling throughout the core logic.
    *   Use a logging framework (e.g., Serilog) to log detailed information, warnings, and errors to a file and/or Windows Event Log.
    *   Provide mechanisms for the UI to display recent log entries.
4.  **Session Management Enhancements:**
    *   UI to display active initiator sessions (IQN, IP address, connection time).
    *   UI to allow forced termination of sessions (send Logout PDU or close connection).

## Phase 5: Testing and Refinement

*Goal: Ensure stability, performance, and interoperability.*

1.  **Comprehensive Unit Tests:**
    *   Write unit tests for PDU serialization/deserialization, CHAP logic, SCSI command handlers, LUN management.
2.  **Integration Tests:**
    *   Test UI-Service communication.
    *   Test full login sequence with various initiators.
3.  **Performance Benchmarking:**
    *   Use tools like IOMeter or `fio` (from a Linux initiator) to measure IOPS, throughput, and latency for Read/Write operations on LUNs.
    *   Identify and address performance bottlenecks.
4.  **Stress Testing:**
    *   Multiple initiators connecting simultaneously.
    *   Long-duration I/O operations.
    *   Network interruption scenarios.
5.  **Interoperability Testing:**
    *   Test with Windows iSCSI Initiator (various Windows versions).
    *   Test with Linux iSCSI Initiator (open-iscsi).
    *   (If possible) Test with VMware ESXi iSCSI Initiator.
6.  **Code Review and Refactoring:**
    *   Review code for clarity, efficiency, and adherence to best practices.
    *   Refactor as needed.

## Phase 6: Documentation and Packaging

*Goal: Prepare the application for distribution and use.*

1.  **User Documentation:**
    *   How to install and configure the iSCSI target application.
    *   How to create and manage LUNs.
    *   How to configure security (CHAP).
    *   Troubleshooting common issues.
2.  **Developer Documentation (Internal):**
    *   Code comments explaining complex logic.
    *   Architecture overview.
3.  **Packaging/Deployment:**
    *   Create an installer (e.g., using WiX Toolset, MSIX, or a Visual Studio Setup Project) that:
        *   Installs the Windows Service.
        *   Installs the UI application.
        *   Sets up necessary permissions.
        *   Creates firewall rules if needed.

## Key Files and Classes (Illustrative Structure - C#)

*   **`WindowsIscsiTarget.sln`** (Solution File)
*   **`IscsiTarget.Core` (Class Library Project)**
    *   `IscsiTargetServer.cs` (Manages TCP listener and sessions)
    *   `IscsiSession.cs` (Handles a single initiator connection, PDU processing, login, SCSI commands)
    *   `Pdu` (Folder for PDU classes like `LoginRequestPDU.cs`, `ScsiCommandPDU.cs`, etc.)
    *   `Scsi` (Folder for SCSI command handlers and related logic)
    *   `Storage` (Folder)
        *   `IStorageBackend.cs` (Interface for storage)
        *   `VhdxStorageBackend.cs` (VHDX file-based storage implementation)
        *   `Lun.cs` (Represents a LUN)
        *   `LunManager.cs` (Manages LUNs)
    *   `Configuration` (Folder)
        *   `TargetConfiguration.cs` (Target settings, CHAP users, LUN mappings)
        *   `ConfigSerializer.cs` (Loads/saves configuration)
    *   `Security` (Folder)
        *   `ChapAuthenticator.cs`
*   **`IscsiTarget.Service` (Windows Service Project)**
    *   `IscsiService.cs` (Inherits from `ServiceBase`, hosts `IscsiTargetServer`)
    *   `ProjectInstaller.cs` (For service installation)
    *   `IpcServer.cs` (e.g., Named Pipe server for UI communication)
*   **`IscsiTarget.UI` (WPF/UWP Project)**
    *   `MainWindow.xaml` / `MainPage.xaml`
    *   `ViewModels` (Folder for MVVM ViewModels)
    *   `Views` (Folder for UserControls/Pages for LUNs, Settings, etc.)
    *   `IpcClient.cs` (e.g., Named Pipe client)
*   **`IscsiTarget.Shared` (Class Library Project - Optional)**
    *   Data transfer objects (DTOs) for IPC.
    *   Common utility classes.

This detailed task list should provide a clear path for implementation. Remember to consult RFC 7143 (iSCSI) and relevant SCSI command specifications (e.g., SPC, SBC) throughout the development process.