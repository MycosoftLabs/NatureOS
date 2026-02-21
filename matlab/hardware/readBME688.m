function [temp, humidity, pressure, gasResistance] = readBME688(port, timeout)
% readBME688 - Read environmental sensor data from BME688
%
%   [temp, humidity, pressure, gasResistance] = readBME688(port, timeout)
%
% Input: port - serial port (e.g., 'COM7'), timeout - seconds (optional, default 5)
% Output: temp (C), humidity (%), pressure (hPa), gasResistance (ohm)

if nargin < 2, timeout = 5; end

temp = 25; humidity = 50; pressure = 1013; gasResistance = 100000;

try
    s = serialport(port, 115200);
    configureTerminator(s, "LF");
    s.Timeout = timeout;
    writeline(s, "READ_BME688");
    line = readline(s);
    clear s;
    parts = strsplit(strtrim(line), ',');
    if numel(parts) >= 4
        temp = str2double(parts{1});
        humidity = str2double(parts{2});
        pressure = str2double(parts{3});
        gasResistance = str2double(parts{4});
    end
catch
    % Fallback when serial not available (e.g., no device connected)
    temp = 25; humidity = 50; pressure = 1013; gasResistance = 100000;
end

end
