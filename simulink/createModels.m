% createModels - Generate NatureOS Simulink models programmatically
% Run this in MATLAB with Simulink to create environmentalControl, irrigationSystem, lightingControl, alertSystem

function createModels()
    baseDir = fileparts(mfilename('fullpath'));
    
    try
        % Environmental control
        mdl = 'environmentalControl';
        new_system(mdl);
        open_system(mdl);
        add_block('simulink/Sources/Constant', [mdl '/TempSetpoint'], 'Value', '25');
        add_block('simulink/Sources/Constant', [mdl '/HumiditySetpoint'], 'Value', '60');
        add_block('simulink/Math Operations/Subtract', [mdl '/TempError']);
        add_block('simulink/Continuous/PID Controller', [mdl '/TempPID']);
        add_block('simulink/Sinks/Out1', [mdl '/HeaterOutput']);
        add_block('simulink/Sinks/Out1', [mdl '/HumidifierOutput']);
        add_line(mdl, 'TempSetpoint/1', 'TempError/1');
        add_line(mdl, 'TempError/1', 'TempPID/1');
        add_line(mdl, 'TempPID/1', 'HeaterOutput/1');
        save_system(mdl, fullfile(baseDir, [mdl '.slx']));
        close_system(mdl, 0);
        
        % Irrigation
        mdl = 'irrigationSystem';
        new_system(mdl);
        open_system(mdl);
        add_block('simulink/Sources/Constant', [mdl '/MoistureSetpoint'], 'Value', '0.4');
        add_block('simulink/Logic and Bit Operations/Compare To Constant', [mdl '/LowMoisture']);
        add_block('simulink/Sinks/Out1', [mdl '/PumpCmd']);
        add_line(mdl, 'MoistureSetpoint/1', 'LowMoisture/1');
        add_line(mdl, 'LowMoisture/1', 'PumpCmd/1');
        save_system(mdl, fullfile(baseDir, [mdl '.slx']));
        close_system(mdl, 0);
        
        % Lighting
        mdl = 'lightingControl';
        new_system(mdl);
        open_system(mdl);
        add_block('simulink/Sources/Clock', [mdl '/Time']);
        add_block('simulink/Logic and Bit Operations/Compare To Constant', [mdl '/DayPeriod']);
        add_block('simulink/Sinks/Out1', [mdl '/LightsOn']);
        add_line(mdl, 'Time/1', 'DayPeriod/1');
        add_line(mdl, 'DayPeriod/1', 'LightsOn/1');
        save_system(mdl, fullfile(baseDir, [mdl '.slx']));
        close_system(mdl, 0);
        
        % Alert system
        mdl = 'alertSystem';
        new_system(mdl);
        open_system(mdl);
        add_block('simulink/Sources/Constant', [mdl '/Sensor1']);
        add_block('simulink/Sources/Constant', [mdl '/Sensor2']);
        add_block('simulink/Logic and Bit Operations/Logical Operator', [mdl '/OR'], 'Operator', 'OR');
        add_block('simulink/Sinks/Out1', [mdl '/Alert']);
        add_line(mdl, 'Sensor1/1', 'OR/1');
        add_line(mdl, 'Sensor2/1', 'OR/2');
        add_line(mdl, 'OR/1', 'Alert/1');
        save_system(mdl, fullfile(baseDir, [mdl '.slx']));
        close_system(mdl, 0);
        
        fprintf('Created all Simulink models in %s\n', baseDir);
    catch e
        warning('Simulink model creation failed (Simulink may not be installed): %s', e.message);
    end
end
