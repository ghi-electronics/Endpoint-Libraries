// CAN on Endpoint is .NET IoT's CanRaw (Iot.Device.SocketCan), which this
// package brings, with EPM815.Can.Initialize in GHIElectronics.Endpoint.Core
// to set a port up. Classic CAN needs nothing else here.
//
// TODO: CAN FD (up to 64 data bytes, a faster data phase). CanRaw sends and
// receives classic frames only: add a wrapper of the standard CanRaw with
// extra arguments for FD (frames with the FD and bitrate-switch flags), and
// a data bitrate in EPM815.Can.Initialize ("dbitrate ... fd on").
