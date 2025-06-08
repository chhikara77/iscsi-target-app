using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IscsiTarget.Core.Pdu;
using IscsiTarget.Core.Storage; // Added for LUN management
using IscsiTarget.Core.Pdu.Login;
using IscsiTarget.Core.Pdu.Logout;
using IscsiTarget.Core.Pdu.Nop;
using IscsiTarget.Core.Pdu.Scsi;
using IscsiTarget.Core.Configuration; // Added for TargetConfiguration
using Serilog; // Added for logging

namespace IscsiTarget.Core
{
    public enum SessionPhase
    {
        SecurityNegotiation,
        LoginOperationalNegotiation,
        FullFeature,
        LoggedOut // Added for clarity after logout
    }

    public class IscsiSession
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly LunManager _lunManager; // Added LunManager
        private readonly TargetConfiguration _configuration; // Added TargetConfiguration
        private SessionPhase _currentPhase;
        private bool _isRunning;
        private readonly string _sessionId; // Added for logging context

        // Negotiated parameters
        private int _maxRecvDataSegmentLength = 8192; // Default
        private bool _headerDigestEnabled = false;
        private bool _dataDigestEnabled = false;

        public string? InitiatorName { get; private set; }
        public string? TargetName { get; private set; } // Should come from TargetConfiguration

        // public IscsiSession(TcpClient client, TargetConfiguration configuration, LunManager lunManager)
        public IscsiSession(TcpClient client, LunManager lunManager) // Modified constructor
        {
            _client = client;
            _stream = client.GetStream();
            _lunManager = lunManager; // Initialize LunManager
            _configuration = configuration; // Initialize TargetConfiguration
            TargetName = _configuration.TargetNameIQN;
            _currentPhase = SessionPhase.SecurityNegotiation; // Or directly to LoginOperationalNegotiation if no security
            _isRunning = true;
            _sessionId = Guid.NewGuid().ToString().Substring(0, 8); // Short unique ID for session logging
        }

        public async Task HandleConnectionAsync(CancellationToken cancellationToken)
        {
            Log.Information($"[{_sessionId}] Session started for {((System.Net.IPEndPoint)_client.Client.RemoteEndPoint!).Address}");
            try
            {
                while (_isRunning && !cancellationToken.IsCancellationRequested && _client.Connected)
                {
                    // Read BHS (first 48 bytes)
                    byte[] bhsBuffer = new byte[48];
                    int bytesRead = await ReadExactlyAsync(bhsBuffer, 0, 48, cancellationToken);
                    if (bytesRead < 48)
                    {
                        Log.Warning($"[{_sessionId}] Failed to read BHS or connection closed by initiator.");
                        break;
                    }

                    // TODO: Implement proper PDU parsing factory or switch based on Opcode
                    // For now, let's assume we can determine PDU type and length
                    BasePDU receivedPdu = ParseBHS(bhsBuffer);

                    // Read remaining PDU parts (AHS, Data Segment) based on DataSegmentLength in BHS
                    if (receivedPdu.DataSegmentLength > 0)
                    {
                        byte[] dataSegmentBuffer = new byte[receivedPdu.DataSegmentLength];
                        bytesRead = await ReadExactlyAsync(dataSegmentBuffer, 0, receivedPdu.DataSegmentLength, cancellationToken);
                        if (bytesRead < receivedPdu.DataSegmentLength)
                        {
                            Log.Warning($"[{_sessionId}] Failed to read PDU Data Segment or connection closed.");
                            break;
                        }
                        // TODO: Attach dataSegmentBuffer to receivedPdu or pass it to a full parse method
                        // For example: receivedPdu.ParseDataSegment(dataSegmentBuffer);
                    }
                    
                    // TODO: Handle Header and Data Digests if enabled
                    if (receivedPdu is ScsiCommandPDU scsiCommandPdu && receivedPdu.DataSegmentLength > 0)
                    {
                        // If it's a SCSI command and has a data segment (e.g., for WRITE commands),
                        // we need to ensure the data segment is properly associated with the PDU.
                        // Assuming ParseBHS only parses BHS and DataSegmentLength is set.
                        // The actual data needs to be read and attached.
                        // This part might need refinement based on how PDU parsing is fully implemented.
                        // For now, let's assume the data segment is read into a buffer and needs to be passed to the PDU.
                        byte[] dataSegmentBuffer = new byte[receivedPdu.DataSegmentLength];
                        bytesRead = await ReadExactlyAsync(dataSegmentBuffer, 0, receivedPdu.DataSegmentLength, cancellationToken);
                        if (bytesRead < receivedPdu.DataSegmentLength)
                        {
                            Log.Warning($"[{_sessionId}] Failed to read PDU Data Segment for SCSI command or connection closed.");
                            break;
                        }
                        scsiCommandPdu.DataSegment = dataSegmentBuffer; // Attach data segment
                    }

                    await ProcessPduAsync(receivedPdu, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Information($"[{_sessionId}] Session handling cancelled.");
            }
            catch (System.IO.IOException ex) when (ex.InnerException is SocketException se && (se.SocketError == SocketError.ConnectionReset || se.SocketError == SocketError.ConnectionAborted))
            {
                Log.Warning(ex, $"[{_sessionId}] IO Exception: Connection closed by client.");
            }
            catch (System.IO.IOException ex)
            {
                Log.Warning(ex, $"[{_sessionId}] IO Exception in session. Client likely disconnected.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[{_sessionId}] Error in session");
            }
            finally
            {
                CloseSession();
            }
        }

        private async Task<int> ReadExactlyAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = await _stream.ReadAsync(buffer, offset + totalBytesRead, count - totalBytesRead, cancellationToken);
                if (bytesRead == 0) // Stream closed
                {
                    return totalBytesRead; 
                }
                totalBytesRead += bytesRead;
            }
            return totalBytesRead;
        }

        private BasePDU ParseBHS(byte[] bhsBuffer)
        {
            // Simplified BHS parsing to determine Opcode and DataSegmentLength
            // A real implementation would use a factory or more robust parsing
            PduOpcode opcode = (PduOpcode)(bhsBuffer[0] & 0x3F);
            int dataSegmentLength = (bhsBuffer[5] << 16) | (bhsBuffer[6] << 8) | bhsBuffer[7];

            BasePDU pdu;
            switch (opcode)
            {
                case PduOpcode.LoginRequest:
                    pdu = new LoginRequestPDU();
                    break;
                case PduOpcode.NopOut:
                    pdu = new NopOutPDU();
                    break;
                case PduOpcode.ScsiCommand: // Added case for ScsiCommandPDU
                    pdu = new ScsiCommandPDU();
                    break;
                case PduOpcode.LogoutRequest: // Added case for LogoutRequestPDU
                    pdu = new LogoutRequestPDU();
                    break;
                // Add other PDU types as needed
                default:
                    Log.Warning($"[{_sessionId}] Received unknown or unhandled PDU Opcode: {opcode}");
                    // Create a generic BasePDU or throw an error
                    throw new NotImplementedException($"PDU Opcode {opcode} not implemented or not expected here.");
            }
            pdu.Deserialize(bhsBuffer); // This will parse the BHS part
            // Note: DataSegmentLength is already part of BasePDU parsing
            return pdu;
        }

        private async Task ProcessPduAsync(BasePDU pdu, CancellationToken cancellationToken)
        {
            Log.Debug($"[{_sessionId}] Processing PDU: {pdu.Opcode}");
            switch (pdu.Opcode)
            {
                case PduOpcode.LoginRequest:
                    if (_currentPhase == SessionPhase.SecurityNegotiation || _currentPhase == SessionPhase.LoginOperationalNegotiation)
                    {
                        await HandleLoginRequestAsync(pdu as LoginRequestPDU, cancellationToken);
                    }
                    else
                    {
                        Log.Warning($"[{_sessionId}] Received LoginRequest PDU in invalid phase: {_currentPhase}");
                        // Send error response
                    }
                    break;

                case PduOpcode.NopOut:
                    await HandleNopOutAsync(pdu as NopOutPDU, cancellationToken);
                    break;

                case PduOpcode.ScsiCommand:
                    if (_currentPhase == SessionPhase.FullFeature)
                    {
                        await HandleScsiCommandAsync(pdu as ScsiCommandPDU, cancellationToken);
                    }
                    else
                    {
                        Log.Warning($"[{_sessionId}] Received ScsiCommand PDU in invalid phase: {_currentPhase}");
                        // Send error or reject
                    }
                    break;

                case PduOpcode.LogoutRequest:
                    await HandleLogoutRequestAsync(pdu as LogoutRequestPDU, cancellationToken);
                    break;

                // Add other PDU types as needed
                default:
                    Log.Warning($"[{_sessionId}] Received unhandled PDU Opcode in ProcessPduAsync: {pdu.Opcode}");
                    // Potentially send an error response or close connection
                    break;
            }
        }

        private Task HandleLoginRequestAsync(LoginRequestPDU pdu, CancellationToken cancellationToken)
        {
            // Placeholder implementation
            Log.Information($"[{_sessionId}] HandleLoginRequestAsync called.");
            if (pdu.CurrentStageNumber == 0 && pdu.NextStageNumber == 1) // Security Negotiation
            {
                return HandleLoginSecurityNegotiationAsync(pdu, cancellationToken);
            }
            else if (pdu.CurrentStageNumber == 1 && (pdu.NextStageNumber == 1 || pdu.NextStageNumber == 3)) // Operational Negotiation
            {
                return HandleLoginOperationalNegotiationAsync(pdu, cancellationToken);
            }
            else
            {
                Log.Warning($"[{_sessionId}] Invalid login stage in HandleLoginRequestAsync: CSG={pdu.CurrentStageNumber}, NSG={pdu.NextStageNumber}");
                // Send error
                return Task.CompletedTask;
            }
        }

        private async Task HandleNopOutAsync(NopOutPDU nopOut, CancellationToken cancellationToken)
        {
            // Placeholder implementation
            Log.Information($"[{_sessionId}] HandleNopOutAsync called.");
            if (nopOut.InitiatorTaskTag != 0xFFFFFFFF && nopOut.TargetTransferTag != 0xFFFFFFFF)
            {
                // This is a NOP-Out used for ping
                var nopIn = new NopInPDU
                {
                    InitiatorTaskTag = nopOut.InitiatorTaskTag,
                    TargetTransferTag = nopOut.TargetTransferTag, // Echo back TargetTransferTag if it was set
                    Lun = nopOut.Lun
                };
                // If NOP-Out contained data, NOP-In should echo it back.
                // For simplicity, we are not handling data segment here.
                await SendPduAsync(nopIn, cancellationToken);
            }
            else
            {
                // This could be a NOP-Out to confirm connection is alive, no response needed unless I_T_T is not 0xFFFFFFFF
                Log.Debug($"[{_sessionId}] Received NOP-Out likely for connection keep-alive (ITT=0x{nopOut.InitiatorTaskTag:X8}, TTT=0x{nopOut.TargetTransferTag:X8}). No response sent.");
            }
        }

        private async Task HandleLogoutRequestAsync(LogoutRequestPDU logoutRequest, CancellationToken cancellationToken)
        {
            // Placeholder implementation
            Log.Information($"[{_sessionId}] HandleLogoutRequestAsync called. Reason: {logoutRequest.ReasonCode}");
            // Perform logout procedures
            _isRunning = false;
            _currentPhase = SessionPhase.LoggedOut;

            var logoutResponse = new LogoutResponsePDU
            {
                InitiatorTaskTag = logoutRequest.InitiatorTaskTag,
                Response = LogoutResponseCode.Success
            };
            await SendPduAsync(logoutResponse, cancellationToken);
            Log.Information($"[{_sessionId}] Session logged out successfully.");
            // Close connection or signal to close
            _client.Close();
        }

        private async Task HandleLoginSecurityNegotiationAsync(LoginRequestPDU loginRequest, CancellationToken cancellationToken)
        {
            Log.Information($"[{_sessionId}] Handling Login Security Negotiation...");
            InitiatorName = loginRequest.GetParameter(PduConstants.KeyInitiatorName);
            Log.Information($"[{_sessionId}] Initiator Name: {InitiatorName}");

            if (loginRequest.CurrentStageNumber != 0 || loginRequest.NextStageNumber != 1) // CSG=0 (SecNeg), NSG=1 (OpNeg)
            {
                Log.Warning($"[{_sessionId}] Invalid login stage for Security Negotiation.");
                await SendLoginResponseAsync(loginRequest, LoginStatusCode.InitiatorError, true, cancellationToken);
                _isRunning = false;
                return;
            }

            string? authMethodParam = loginRequest.GetParameter(PduConstants.KeyAuthMethod);
            bool chapOffered = authMethodParam?.Split(',').Contains(PduConstants.AuthMethodCHAP) ?? false;
            bool targetRequiresChap = _configuration.InitiatorChapCredentials.Any(); // Simplified: if any CHAP user is configured, CHAP is an option

            if (_chapStep == ChapAuthStep.None)
            {
                if (chapOffered && targetRequiresChap)
                {
                    Log.Information($"[{_sessionId}] CHAP offered by initiator and configured by target. Initiating CHAP.");
                    _chapIdentifier = (byte)new Random().Next(0, 256);
                    _chapChallengeValue = ChapAuthenticator.GenerateChallenge(_chapIdentifier, out _, _configuration.TargetNameIQN); // Name is target's name for challenge
                    
                    var chapResponse = new LoginResponsePDU
                    {
                        TransitFlag = false, // Stay in Security Negotiation
                        ContinueFlag = true,
                        CurrentStageNumber = 0, // SecurityNegotiation
                        NextStageNumber = 1,    // LoginOperationalNegotiation
                        InitiatorSessionID = loginRequest.InitiatorSessionID,
                        TargetSessionIdentifyingHandle = 0x0001, // Example TSID, should be unique
                        StatusClass = (byte)LoginStatusCode.Success,
                        StatusDetail = 0
                    };
                    chapResponse.AddOrUpdateParameter(PduConstants.KeyAuthMethod, PduConstants.AuthMethodCHAP);
                    chapResponse.AddOrUpdateParameter(PduConstants.KeyCHAPAlgorithm, "5"); // MD5
                    chapResponse.AddOrUpdateParameter(PduConstants.KeyCHAPIdent, _chapIdentifier.ToString());
                    chapResponse.AddOrUpdateParameter(PduConstants.KeyCHAPChallenge, Convert.ToBase64String(_chapChallengeValue)); // Or hex, RFC uses binary
                    // TargetAlias might be sent here too
                    chapResponse.AddOrUpdateParameter(PduConstants.KeyTargetName, _configuration.TargetNameIQN);

                    await SendPduAsync(chapResponse, cancellationToken);
                    _chapStep = ChapAuthStep.ChallengeSent;
                }
                else if (!targetRequiresChap || authMethodParam?.Split(',').Contains(PduConstants.AuthMethodNone) == true)
                {
                    Log.Information($"[{_sessionId}] No CHAP required or None accepted. Proceeding to Operational Negotiation.");
                    _currentPhase = SessionPhase.LoginOperationalNegotiation;
                    var opResponse = new LoginResponsePDU
                    {
                        TransitFlag = true, // Transition to OpNeg
                        ContinueFlag = true, // More negotiation in OpNeg
                        CurrentStageNumber = 1, // LoginOperationalNegotiation
                        NextStageNumber = 1,    // LoginOperationalNegotiation
                        InitiatorSessionID = loginRequest.InitiatorSessionID,
                        TargetSessionIdentifyingHandle = 0x0001,
                        StatusClass = (byte)LoginStatusCode.Success,
                        StatusDetail = 0
                    };
                    opResponse.AddOrUpdateParameter(PduConstants.KeyAuthMethod, PduConstants.AuthMethodNone);
                    opResponse.AddOrUpdateParameter(PduConstants.KeyTargetName, _configuration.TargetNameIQN);
                    await SendPduAsync(opResponse, cancellationToken);
                }
                else
                {
                    Log.Warning($"[{_sessionId}] Auth method mismatch or CHAP required but not offered with None.");
                    await SendLoginResponseAsync(loginRequest, LoginStatusCode.AuthenticationFailure, true, cancellationToken);
                    _isRunning = false;
                }
            }
            else if (_chapStep == ChapAuthStep.ChallengeSent)
            {
                Log.Information($"[{_sessionId}] Received CHAP response from initiator.");
                string? chapName = loginRequest.GetParameter(PduConstants.KeyCHAPName);
                string? chapResponseB64 = loginRequest.GetParameter(PduConstants.KeyCHAPResponse);
                string? chapIdStr = loginRequest.GetParameter(PduConstants.KeyCHAPIdent);

                if (string.IsNullOrEmpty(chapName) || string.IsNullOrEmpty(chapResponseB64) || string.IsNullOrEmpty(chapIdStr) || _chapChallengeValue == null)
                {
                    Log.Warning($"[{_sessionId}] Missing CHAP parameters in initiator's response.");
                    await SendLoginResponseAsync(loginRequest, LoginStatusCode.AuthenticationFailure, true, cancellationToken);
                    _isRunning = false; return;
                }

                if (!byte.TryParse(chapIdStr, out byte receivedChapId) || receivedChapId != _chapIdentifier)
                {
                    Log.Warning($"[{_sessionId}] CHAP Identifier mismatch.");
                    await SendLoginResponseAsync(loginRequest, LoginStatusCode.AuthenticationFailure, true, cancellationToken);
                    _isRunning = false; return;
                }

                ChapInitiatorCredential? cred = _configuration.InitiatorChapCredentials.FirstOrDefault(c => c.InitiatorName == chapName);
                if (cred == null)
                {
                    Log.Warning($"[{_sessionId}] No CHAP credentials found for initiator: {chapName}");
                    await SendLoginResponseAsync(loginRequest, LoginStatusCode.AuthenticationFailure, true, cancellationToken);
                    _isRunning = false; return;
                }

                byte[] chapResponseBytes;
                try { chapResponseBytes = Convert.FromBase64String(chapResponseB64); }
                catch { 
                    Log.Warning($"[{_sessionId}] Invalid CHAP response format (not Base64).");
                    await SendLoginResponseAsync(loginRequest, LoginStatusCode.AuthenticationFailure, true, cancellationToken); 
                    _isRunning = false; return;
                }

                bool chapValid = ChapAuthenticator.VerifyResponse(_chapIdentifier, _chapChallengeValue, chapResponseBytes, cred.Secret);

                if (chapValid)
                {
                    Log.Information($"[{_sessionId}] CHAP authentication successful.");
                    _chapInitiatorName = chapName; // Store authenticated initiator name
                    _currentPhase = SessionPhase.LoginOperationalNegotiation;
                    _chapStep = ChapAuthStep.None; // Reset CHAP state

                    var successResponse = new LoginResponsePDU
                    {
                        TransitFlag = true, // Transition to OpNeg
                        ContinueFlag = true, // More negotiation in OpNeg
                        CurrentStageNumber = 1, // LoginOperationalNegotiation
                        NextStageNumber = 1,    // LoginOperationalNegotiation
                        InitiatorSessionID = loginRequest.InitiatorSessionID,
                        TargetSessionIdentifyingHandle = 0x0001,
                        StatusClass = (byte)LoginStatusCode.Success,
                        StatusDetail = 0
                    };
                    successResponse.AddOrUpdateParameter(PduConstants.KeyTargetName, _configuration.TargetNameIQN);
                    await SendPduAsync(successResponse, cancellationToken);
                }
                else
                {
                    Log.Warning($"[{_sessionId}] CHAP authentication failed.");
                    await SendLoginResponseAsync(loginRequest, LoginStatusCode.AuthenticationFailure, true, cancellationToken);
                    _isRunning = false;
                }
            }
            else
            {
                 Log.Warning($"[{_sessionId}] Unexpected CHAP step {_chapStep} during Security Negotiation.");
                 await SendLoginResponseAsync(loginRequest, LoginStatusCode.TargetError, true, cancellationToken);
                 _isRunning = false;
            }
        }

        private async Task HandleLoginOperationalNegotiationAsync(LoginRequestPDU loginRequest, CancellationToken cancellationToken)
        {
            Log.Information($"[{_sessionId}] Handling Login Operational Negotiation...");
            // Process operational parameters like MaxRecvDataSegmentLength, etc.
            // For now, just acknowledge and move to Full Feature Phase if NSG is FF.

            if (loginRequest.CurrentStageNumber != 1 || (loginRequest.NextStageNumber != 1 && loginRequest.NextStageNumber != 3))
            {
                Log.Warning($"[{_sessionId}] Invalid login stage for Operational Negotiation.");
                await SendLoginResponseAsync(loginRequest, LoginStatusCode.InitiatorError, true, cancellationToken);
                _isRunning = false;
                return;
            }

            // Example: Acknowledge initiator's parameters or propose target's.
            // Here we'll just accept and transition if initiator wants to go to Full Feature.
            string? maxRecvDSL = loginRequest.GetParameter(PduConstants.KeyMaxRecvDataSegmentLength);
            if (maxRecvDSL != null && int.TryParse(maxRecvDSL, out int initiatorMaxRecvDSL))
            {
                _maxRecvDataSegmentLength = Math.Min(_maxRecvDataSegmentLength, initiatorMaxRecvDSL);
            }
            // Handle other parameters like HeaderDigest, DataDigest, MaxConnections, etc.

            LoginResponsePDU opResponse = new LoginResponsePDU
            {
                InitiatorSessionID = loginRequest.InitiatorSessionID,
                TargetSessionIdentifyingHandle = 0x0001, // Use the same TSID
                StatusClass = (byte)LoginStatusCode.Success,
                StatusDetail = 0
            };

            opResponse.AddOrUpdateParameter(PduConstants.KeyTargetName, TargetName!); // Ensure TargetName is not null here
            opResponse.AddOrUpdateParameter(PduConstants.KeyMaxRecvDataSegmentLength, _maxRecvDataSegmentLength.ToString());
            // Add other negotiated parameters back to initiator

            if (loginRequest.TransitFlag && loginRequest.NextStageNumber == 3) // Initiator wants to transition to Full Feature
            {
                opResponse.TransitFlag = true;
                opResponse.ContinueFlag = false;
                opResponse.CurrentStageNumber = 3; // FullFeaturePhase
                opResponse.NextStageNumber = 3;    // FullFeaturePhase
                _currentPhase = SessionPhase.FullFeature;
                Log.Information($"[{_sessionId}] Login Operational Negotiation successful. Transitioning to Full Feature Phase.");
            }
            else // Continue operational negotiation or initiator error
            {
                opResponse.TransitFlag = false; // Or true if target decides to transition
                opResponse.ContinueFlag = true; // Assuming more negotiation might be needed or initiator will send another OpLogin
                opResponse.CurrentStageNumber = 1; // LoginOperationalNegotiation
                opResponse.NextStageNumber = 1;    // LoginOperationalNegotiation
                Log.Information($"[{_sessionId}] Login Operational Negotiation continues...");
            }

            await SendPduAsync(opResponse, cancellationToken);

            if (_currentPhase == SessionPhase.FullFeature)
            {
                Log.Information($"[{_sessionId}] Session now in Full Feature Phase.");
            }
        }

        private async Task SendLoginResponseAsync(LoginRequestPDU? originatingRequest, LoginStatusCode status, bool transitToExit, CancellationToken cancellationToken)
        {
            var response = new LoginResponsePDU
            {
                StatusClass = (byte)status,
                StatusDetail = 0, // Could be more specific
                TransitFlag = transitToExit, // If true, login process terminates
                ContinueFlag = false,
                CurrentStageNumber = originatingRequest?.CurrentStageNumber ?? 0,
                NextStageNumber = originatingRequest?.NextStageNumber ?? 0, // Or a specific exit stage
                InitiatorSessionID = originatingRequest?.InitiatorSessionID ?? new byte[8],
                TargetSessionIdentifyingHandle = 0x0001 // Consistent TSID
            };
            response.AddOrUpdateParameter(PduConstants.KeyTargetName, _configuration.TargetNameIQN); // Ensure _configuration is available
            await SendPduAsync(response, cancellationToken);
        }

        private async Task SendPduAsync(BasePDU pdu, CancellationToken cancellationToken)
        {
            Log.Debug($"[{_sessionId}] Sending PDU: {pdu.Opcode}");
            byte[] pduBytes = pdu.Serialize();
            await _stream.WriteAsync(pduBytes, 0, pduBytes.Length, cancellationToken);
            await _stream.FlushAsync(cancellationToken); // Ensure data is sent immediately
            Log.Verbose($"[{_sessionId}] Sent {pduBytes.Length} bytes for PDU {pdu.Opcode}");
        }

        private bool IsInitiatorAllowedForLun(string initiatorName, ulong lunId) // Corrected signature
        {
            var lunConfig = _configuration.Luns.FirstOrDefault(l => l.LunId == lunId);
            if (lunConfig == null)
            {
                Log.Warning($"[{_sessionId}] LUN ID {lunId} not found in target configuration. Denying access for initiator {initiatorIqnFromSession ?? "Unknown"}.");
                return false; 
            }

            // If AllowedInitiatorIQNs is null or empty, it means the LUN is accessible to all authenticated initiators.
            if (lunConfig.AllowedInitiatorIQNs == null || !lunConfig.AllowedInitiatorIQNs.Any())
            {
                Log.Debug($"[{_sessionId}] LUN {lunId} is accessible to all authenticated initiators.");
                return true; 
            }

            // If LUN has restrictions, but current session's initiator IQN is unknown (e.g. CHAP failed or not used, and InitiatorName not set from Login PDU)
            if (string.IsNullOrEmpty(initiatorIqnFromSession))
            {
                Log.Warning($"[{_sessionId}] LUN {lunId} has access restrictions, but current session's initiator IQN is unknown. Denying access.");
                return false; 
            }

            bool isAllowed = lunConfig.AllowedInitiatorIQNs.Contains(initiatorIqnFromSession, StringComparer.OrdinalIgnoreCase);
            
            if (isAllowed)
            {
                Log.Information($"[{_sessionId}] Initiator {initiatorIqnFromSession} IS authorized for LUN {lunId}.");
            }
            else
            {
                Log.Warning($"[{_sessionId}] Initiator {initiatorIqnFromSession} is NOT authorized for LUN {lunId}. Allowed: [{string.Join(", ", lunConfig.AllowedInitiatorIQNs)}].");
            }
            return isAllowed;
        }

        private async Task HandleScsiCommandAsync(ScsiCommandPDU scsiCommand, CancellationToken cancellationToken)
        {
            Log.Information($"[{_sessionId}] Handling ScsiCommandPDU. Opcode: {scsiCommand.Cdb[0]:X2}, LUN: {scsiCommand.Lun}, Initiator: {InitiatorName ?? "Unknown"}");

            byte lunIdFromPdu = (byte)scsiCommand.Lun; // LUN from iSCSI PDU header

            // LUN Masking Check for non-REPORT LUNS commands
            // REPORT LUNS (0xA0) has its own filtering logic within its case block.
            if (scsiCommand.Cdb[0] != (byte)ScsiOpCode.ReportLuns)
            {
                if (!IsInitiatorAllowedForLun(lunIdFromPdu, InitiatorName))
                {
                    Log.Warning($"[{_sessionId}] Initiator {InitiatorName ?? "Unknown"} not authorized for LUN {lunIdFromPdu} for command {scsiCommand.Cdb[0]:X2}. Sending CheckCondition.");
                    ScsiResponsePDU accessDeniedResponse = new ScsiResponsePDU
                    {
                        InitiatorTaskTag = scsiCommand.InitiatorTaskTag,
                        Status = ScsiStatus.CheckCondition,
                        SenseData = CreateSenseData(SenseKey.IllegalRequest, Asc.LogicalUnitNotSupported, Ascq.Default) // Or a more specific access denied code
                    };
                    await SendPduAsync(accessDeniedResponse, cancellationToken);
                    return;
                }
            }

            Lun? lun = _lunManager.GetLun(lunIdFromPdu);
            // The following block was a duplicate of IsInitiatorAllowedForLun and has been removed.
            // Ensure LUN exists after authorization (especially for REPORT LUNS which might list non-existent but configured LUNs if not careful)
            if (lun == null && scsiCommand.Cdb[0] != (byte)ScsiOpCode.ReportLuns) // REPORT LUNS handles non-existent LUNs by not listing them
            {
                Log.Warning($"[{_sessionId}] LUN {lunIdFromPdu} not found by LunManager after authorization. Command {scsiCommand.Cdb[0]:X2}. Sending CheckCondition.");
                ScsiResponsePDU lunNotFoundResponse = new ScsiResponsePDU
                {
                    InitiatorTaskTag = scsiCommand.InitiatorTaskTag,
                    Status = ScsiStatus.CheckCondition,
                    SenseData = CreateSenseData(SenseKey.IllegalRequest, Asc.LogicalUnitNotSupported, Ascq.Default)
                };
                await SendPduAsync(lunNotFoundResponse, cancellationToken);
                return;
            }

            var responsePdu = new ScsiResponsePDU
            {
                InitiatorTaskTag = scsiCommand.InitiatorTaskTag,
                Status = ScsiStatus.Good // Default to Good, specific handlers can change it
            };

            try
            {
                switch ((ScsiOpCode)scsiCommand.Cdb[0])
                {
                    case ScsiOpCode.TestUnitReady:
                        // Already handled LUN check, if we are here, LUN is accessible and exists.
                        Log.Debug($"[{_sessionId}] TEST UNIT READY for LUN {lunIdFromPdu} successful.");
                        responsePdu.Status = ScsiStatus.Good;
                        break;

                    case ScsiOpCode.Inquiry:
                        Log.Debug($"[{_sessionId}] Handling INQUIRY for LUN {lunIdFromPdu}.");
                        if (lun != null)
                        {
                            responsePdu.DataSegment = lun.HandleInquiry(scsiCommand.Cdb);
                            responsePdu.DataSegmentLength = responsePdu.DataSegment?.Length ?? 0;
                        }
                        else // Should not happen if LUN check above is correct
                        {
                            Log.Error($"[{_sessionId}] INQUIRY: LUN {lunIdFromPdu} was null despite earlier checks.");
                            responsePdu.Status = ScsiStatus.CheckCondition;
                            responsePdu.SenseData = CreateSenseData(SenseKey.IllegalRequest, Asc.LogicalUnitNotSupported, Ascq.Default);
                        }
                        break;

                    case ScsiOpCode.ReportLuns:
                        Log.Information($"[{_sessionId}] Handling REPORT LUNS command (0xA0) for initiator: {InitiatorName ?? "Unknown"}.");
                        List<byte> allConfiguredLunIds = _lunManager.GetConfiguredLunIds();
                        List<byte> accessibleLunIds = new List<byte>();

                        // Determine which LUNs are accessible to this initiator
                        foreach (byte id in allConfiguredLunIds)
                        {
                            if (IsInitiatorAllowedForLun(id, InitiatorName))
                            {
                                accessibleLunIds.Add(id);
                            }
                        }
                        Log.Debug($"[{_sessionId}] Report LUNs: All configured: [{string.Join(",", allConfiguredLunIds)}]. Accessible for {InitiatorName ?? "Unknown"}: [{string.Join(",", accessibleLunIds)}]");

                        int lunListLength = accessibleLunIds.Count * 8; // Each LUN entry is 8 bytes
                        byte[] reportLunsData = new byte[8 + lunListLength]; // 8-byte header for LUN list length field
                        
                        // LUN List Length field (4 bytes, Big Endian), specifies length of LUN entries that follow.
                        reportLunsData[0] = (byte)((lunListLength >> 24) & 0xFF);
                        reportLunsData[1] = (byte)((lunListLength >> 16) & 0xFF);
                        reportLunsData[2] = (byte)((lunListLength >> 8) & 0xFF);
                        reportLunsData[3] = (byte)(lunListLength & 0xFF);
                        // Bytes 4-7 of the header are reserved.

                        int currentOffset = 8; // Start writing LUN entries after the 8-byte header
                        foreach (byte id in accessibleLunIds)
                        {
                            // Standard LUN structure (8 bytes per LUN)
                            // For LUNs < 256, typically LUN ID is in the second byte (offset + 1).
                            // Byte 0: Bits 7-6 Address Method (00b for peripheral device addressing is common)
                            //         Bits 5-0 Bus Identifier (typically 0)
                            // Byte 1: LUN itself
                            // Bytes 2-7: Reserved
                            reportLunsData[currentOffset + 1] = id; // LUN ID
                            // Ensure other bytes in the 8-byte entry are zeroed if not set otherwise
                            reportLunsData[currentOffset + 0] = 0x00; // Address Method & Bus ID
                            for (int j = 2; j < 8; j++)
                            {
                                reportLunsData[currentOffset + j] = 0x00;
                            }
                            currentOffset += 8;
                        }
                        responsePdu.DataSegment = reportLunsData;
                        responsePdu.DataSegmentLength = reportLunsData.Length;
                        break;
                    default:
                        Log.Warning($"[{_sessionId}] Unhandled SCSI OpCode: {scsiCommand.Cdb[0]:X2} for LUN {lunIdFromPdu}.");
                        responsePdu.Status = ScsiStatus.CheckCondition;
                        responsePdu.SenseData = CreateSenseData(SenseKey.IllegalRequest, Asc.InvalidCommandOperationCode, Ascq.Default);
                        break;
                } // End switch
            } // End try
            catch (Exception ex)
            {
                Log.Error(ex, $"[{_sessionId}] Exception processing SCSI command {scsiCommand.Cdb[0]:X2} for LUN {lunIdFromPdu}: {ex.Message}");
                if (responsePdu != null)
                {
                    responsePdu.Status = ScsiStatus.CheckCondition;
                    responsePdu.SenseData = CreateSenseData(SenseKey.AbortedCommand, Asc.InternalTargetFailure, Ascq.Default);
                    responsePdu.DataSegment = null;
                    responsePdu.DataSegmentLength = 0;
                }
            }
            finally
            {
                if (responsePdu != null)
                {
                    await SendPduAsync(responsePdu, cancellationToken);
                }
                else
                {
                     Log.Error($"[{_sessionId}] responsePdu was unexpectedly null before sending in finally block for SCSI command {scsiCommand.Cdb[0]:X2}.");
                }
            }
        } // End method HandleScsiCommandAsync