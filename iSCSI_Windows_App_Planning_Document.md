# Planning Document: iSCSI Windows Application for Storage Sharing

## 1. Introduction and Goals

*   **Objective**: Develop a Windows application that enables sharing of local storage devices with other devices on the network using the iSCSI protocol.
*   **Scope**: The application will act as an iSCSI target, making local drives accessible as block storage to iSCSI initiators.
*   **Target Users**: Users needing to share storage resources across a network, such as for centralized storage, virtual machine disks, or shared data access.

## 2. Core Concepts (Based on Research)

*   **iSCSI (Internet Small Computer System Interface)**: A transport layer protocol that transports SCSI commands over a TCP/IP network. It allows block-level access to storage devices as if they were locally connected. <mcreference link="https://www.techtarget.com/searchstorage/definition/iSCSI" index="1">1</mcreference>
*   **SAN (Storage Area Network)**: A dedicated network that provides access to consolidated, block-level data storage. iSCSI is a common protocol for implementing SANs over Ethernet. <mcreference link="https://www.starwindsoftware.com/blog/complete-an-infrastructure-project-for-your-organization-with-iscsi-san/" index="4">4</mcreference>
*   **iSCSI Initiator**: The client software or hardware that sends SCSI commands to an iSCSI target. (e.g., Windows iSCSI Initiator, Linux open-iscsi). <mcreference link="https://www.techtarget.com/searchstorage/definition/iSCSI" index="1">1</mcreference>
*   **iSCSI Target**: The server (our application) that hosts the storage resources and makes them available to initiators. <mcreference link="https://www.techtarget.com/searchstorage/definition/iSCSI" index="1">1</mcreference>
*   **LUN (Logical Unit Number)**: Represents an individual block storage device (e.g., a hard drive partition) made available by the iSCSI target. <mcreference link="https://docs.netapp.com/us-en/ontap/san-admin/san-host-provisioning-concept.html" index="5">5</mcreference>

## 3. Application Architecture

*   **User Interface (UI)**:
    *   Technology: Windows Presentation Foundation (WPF) or Universal Windows Platform (UWP) for a modern UI.
    *   Functionality:
        *   Select local drives/partitions to share.
        *   Configure LUNs (mapping local drives to LUNs).
        *   Manage iSCSI target settings (e.g., target name, port).
        *   Configure security (CHAP authentication).
        *   Monitor connected initiators and I/O activity.
        *   Start/Stop iSCSI target service.
*   **Core Logic (Backend Service - Windows Service)**:
    *   **iSCSI Protocol Handler**:
        *   Listen for incoming iSCSI connections (default TCP port 3260). <mcreference link="https://www.techtarget.com/searchdatacenter/tip/Creating-a-reliable-and-fast-SAN-network-design-with-iSCSI" index="2">2</mcreference>
        *   Parse iSCSI Protocol Data Units (PDUs).
        *   Process SCSI commands (Read, Write, Inquiry, etc.) encapsulated in iSCSI PDUs.
        *   Manage iSCSI sessions and connections.
    *   **Storage Abstraction Layer**:
        *   Interface with local storage devices (physical drives, partitions, VHD/VHDX files).
        *   Perform block-level read/write operations on the selected local storage.
        *   Handle LUN mapping and management.
    *   **Configuration Manager**:
        *   Store and retrieve application settings (shared drives, LUNs, security settings).
        *   Likely use XML, JSON, or Windows Registry for configuration storage.
    *   **Security Module**:
        *   Implement CHAP (Challenge-Handshake Authentication Protocol) for initiator authentication. <mcreference link="https://www.starwindsoftware.com/blog/complete-an-infrastructure-project-for-your-organization-with-iscsi-san/" index="4">4</mcreference>
        *   Potentially IP-based access control lists.
*   **Communication**: UI will communicate with the backend service via Inter-Process Communication (IPC), such as Named Pipes or WCF.

## 4. Key Features to Implement

*   **iSCSI Target Functionality**:
    *   Discovery (SendTargets).
    *   Login/Logout processing.
    *   SCSI command processing (Read(10/12/16), Write(10/12/16), Inquiry, Test Unit Ready, Report LUNs, Mode Sense/Select, Read Capacity).
    *   Task Management Functions.
*   **LUN Management**:
    *   Ability to select physical disks, partitions, or VHD/VHDX files as LUNs.
    *   Dynamic LUN provisioning (add/remove LUNs without restarting service).
*   **Security**:
    *   CHAP authentication (mutual CHAP is a plus).
    *   Initiator IQN (iSCSI Qualified Name) filtering.
*   **Networking**:
    *   Configurable listening IP address and port.
    *   Support for multiple network interfaces.
*   **User Experience**:
    *   Intuitive UI for configuration and monitoring.
    *   Logging for troubleshooting.

## 5. Development and Technology Stack

*   **Programming Language**: C# (for UI and core logic if high performance is not the absolute bottleneck) or C++ (for performance-critical backend components, especially the iSCSI protocol handler and storage I/O).
*   **Frameworks/Libraries**:
    *   .NET Framework / .NET Core (for C#).
    *   Windows API (for low-level disk access, service management).
    *   Potentially a third-party iSCSI target library if building from scratch is too complex, though the request implies building it.
*   **Development Tools**: Visual Studio.
*   **Version Control**: Git.

## 6. Network Considerations for Deployment

*   **Dedicated Network/VLAN**: For optimal performance and security, iSCSI traffic should be isolated on its own network segment or VLAN. <mcreference link="https://www.networkcomputing.com/data-centers/how-plan-iscsi-san" index="3">3</mcreference>
*   **Gigabit Ethernet**: Minimum requirement; 10GbE or faster recommended for performance-intensive workloads.
*   **Jumbo Frames**: Configure network adapters and switches to support jumbo frames (MTU 9000) to reduce overhead and improve throughput. <mcreference link="https://www.techtarget.com/searchdatacenter/tip/Creating-a-reliable-and-fast-SAN-network-design-with-iSCSI" index="2">2</mcreference>
*   **Switches**: Use enterprise-class, non-blocking switches. <mcreference link="https://www.networkcomputing.com/data-centers/how-plan-iscsi-san" index="3">3</mcreference>
*   **Multipath I/O (MPIO)**: For redundancy and load balancing, though implementing MPIO on the target side adds complexity. Initially, focus on a single path. <mcreference link="https://www.networkcomputing.com/data-centers/how-plan-iscsi-san" index="3">3</mcreference>

## 7. Security Considerations

*   **CHAP Authentication**: Essential to verify initiator identity.
*   **IPsec**: While powerful, IPsec for iSCSI can be complex to configure and might have performance overhead. CHAP is the more common first line of defense. <mcreference link="https://www.networkcomputing.com/data-centers/how-plan-iscsi-san" index="3">3</mcreference>
*   **Network Isolation**: Segregating iSCSI traffic is a primary security measure.
*   **Principle of Least Privilege**: The Windows service should run with the minimum necessary permissions.

## 8. Project Phases and Milestones

1.  **Phase 1: Core Protocol Implementation (Proof of Concept)**
    *   Basic iSCSI PDU parsing.
    *   Implement Discovery (SendTargets).
    *   Implement Login/Logout.
    *   Basic SCSI Inquiry and Read Capacity for a single, hardcoded LUN (e.g., a small file).
    *   Test with Windows iSCSI Initiator.
2.  **Phase 2: Storage Abstraction and Basic LUN Management**
    *   Develop a layer to read/write blocks from local physical disks/partitions.
    *   Allow UI to select a single local disk/partition to share.
    *   Implement SCSI Read/Write commands.
3.  **Phase 3: UI Development and Service Integration**
    *   Develop the basic UI for LUN configuration and service control.
    *   Implement the Windows Service structure.
    *   Establish IPC between UI and service.
4.  **Phase 4: Advanced Features and Security**
    *   Implement CHAP authentication.
    *   Support for VHD/VHDX files as LUNs.
    *   Multiple LUN support.
    *   Logging and error handling.
5.  **Phase 5: Testing and Refinement**
    *   Performance testing.
    *   Stability testing.
    *   Interoperability testing with different initiators (if possible).
    *   Bug fixing and UI polishing.
6.  **Phase 6: Documentation and Packaging**
    *   User manual.
    *   Installer.

## 9. Potential Challenges

*   **Complexity of iSCSI Protocol**: The iSCSI protocol has many nuances.
*   **Performance Optimization**: Achieving high performance for block I/O over the network.
*   **Low-Level Disk I/O**: Requires careful handling to avoid data corruption.
*   **Windows Service Development**: Debugging and managing services can be tricky.
*   **Security Implementation**: Ensuring robust and correct security mechanisms.

## 10. Testing Strategy

*   **Unit Tests**: For individual components (PDU parsing, SCSI command handlers, etc.).
*   **Integration Tests**: Test interaction between UI, service, and storage.
*   **End-to-End Tests**: Use standard iSCSI initiators (Windows, Linux) to connect to the target, format LUNs, read/write data, and test stability.
*   **Performance Benchmarking**: Tools like IOMeter or CrystalDiskMark on the initiator side against the LUNs.
*   **Stress Testing**: Sustained I/O operations, multiple concurrent initiators (if supported).

This document should provide a solid foundation for an AI coder to understand the project's requirements and architecture.