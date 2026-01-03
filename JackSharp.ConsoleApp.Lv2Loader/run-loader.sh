#!/bin/bash
export JACK_PROMISCUOUS_SERVER=jack 
export JACK_NO_AUDIO_RESERVATION=1 
#export LD_LIBRARY_PATH=/usr/local/lib:$LD_LIBRARY_PATH 
cd /home/pistomp/JackSharpCore/JackSharp.ConsoleApp.Lv2Loader 
dotnet run
