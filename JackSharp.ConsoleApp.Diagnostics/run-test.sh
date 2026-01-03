#!/bin/bash
export JACK_PROMISCUOUS_SERVER=jack 
export JACK_NO_AUDIO_RESERVATION=1 
#export LD_LIBRARY_PATH=/usr/local/lib:$LD_LIBRARY_PATH 
cd /home/pistomp/JackSharpCore/JackSharp.ConsoleApp.Diagnostics 
dotnet run --monitor-levels #don't add head/tail/grep/console redirection here, this console app will exit after monitoring levels. 
#dotnet run --noise-floor  #don't add head/tail/grep/console redirection here, this console app will exit after monitoring levels. 
#dotnet run --audio-quality
#dotnet run --loopback

# amixer cset numid=21 28
# amixer cset numid=26 10.0


# amixer cset numid=21 2
# amixer cset numid=26 0.0
# amixer cset numid=25 0.0