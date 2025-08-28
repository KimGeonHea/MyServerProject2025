SET curPath=%cd%
SET projectPath=%1

CD %projectPath%
CD ..

SET rootPath=%cd%
SET outputPath=%rootPath%%2

CD Common\Protocol

protoc.exe -I=./ --csharp_out=%outputPath% ./Protocol.proto ./Enum.proto ./Struct.proto
IF ERRORLEVEL 1 PAUSE

CD %rootPath%\Tools\PacketGenerator\bin
START PacketGenerator.exe -o %2 -t %3

CD %curPath%

EXIT