function [ack, response] = sendCommand(port, command, timeout)
% sendCommand - Device command interface for MycoBrain
%
%   [ack, response] = sendCommand(port, command, timeout)
%
% Input: port - serial port, command - string (e.g., 'LED_ON', 'READ_BME688'), timeout (optional)
% Output: ack - true if command acknowledged, response - raw response string

if nargin < 3, timeout = 5; end
ack = false;
response = '';

try
    s = serialport(port, 115200);
    configureTerminator(s, "LF");
    s.Timeout = timeout;
    writeline(s, command);
    response = readline(s);
    clear s;
    ack = contains(response, 'OK') || contains(response, 'ACK');
catch
    response = 'ERR:No device';
end

end
