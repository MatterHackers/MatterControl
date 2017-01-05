# Copyright (c) 2015, Lars Brubaker
# All rights reserved.
# 
# Redistribution and use in source and binary forms, with or without
# modification, are permitted provided that the following conditions are met: 
# 
# 1. Redistributions of source code must retain the above copyright notice, this
#    list of conditions and the following disclaimer. 
# 2. Redistributions in binary form must reproduce the above copyright notice,
#    this list of conditions and the following disclaimer in the documentation
#    and/or other materials provided with the distribution. 
# 
# THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
# ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
# DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
# ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
# (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
# LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
# ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
# (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
# SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
# 
# The views and conclusions contained in the software and documentation are those
# of the authors and should not be interpreted as representing official policies, 
# either expressed or implied, of the FreeBSD Project.

# This is to test connection and printing. We use it with com0com 
# to validate MatterControl under various situations.
from time import sleep
import sys
import argparse
import serial
import time
import random

extruderGoalTemperature = 210
bedGoalTemperature = -1 # no bed present

"""Add response callbacks here"""
def randomTemp(command):
	# temp commands look like this: ok T:19.4 /0.0 B:0.0 /0.0 @:0 B@:0
    if bedGoalTemperature == -1:
	    return "ok T:%s\n" % (extruderGoalTemperature + random.randrange(-2,2))
    else:
        return "ok T:%s B:%s\n" % (extruderGoalTemperature + random.randrange(-2,2), bedGoalTemperature + random.randrange(-2,2))

def getPosition(command):
	# temp commands look like this: X:0.00 Y:0.00 Z0.00 E:0.00 Count X: 0.00 Y:0.00 Z:0.00 then an ok on the next line
	return "X:0.00 Y:0.00 Z0.00 E:0.00 Count X: 0.00 Y:0.00 Z:0.00\nok\n"

def reportMarlinFirmware(command):
	return 'FIRMWARE_NAME:Marlin V1; Sprinter/grbl mashup for gen6 FIRMWARE_URL:https://github.com/MarlinFirmware/Marlin PROTOCOL_VERSION:1.0 MACHINE_TYPE:Framelis v1 EXTRUDER_COUNT:1 UUID:155f84b5-d4d7-46f4-9432-667e6876f37a\nok\n'

def echo(command): 
	return command
	
def parseChecksumLine(command):
	if command[0] == 'N':
		spaceIndex = command.find(' ') + 1
		endIndex = command.find('*')
		return command[spaceIndex:endIndex]
	else:
		return command

def getCommandKey(command):
	if command.find(' ') != -1:
		return command[:command.find(' ')]
	else:
		return command

def getCorrectResponse(command):
	try:
		#Remove line returns
		command = ''.join(command.splitlines()) # strip of the trailing cr (\n)
		command = parseChecksumLine(command)
		commandKey = getCommandKey(command)
		if commandKey in responses:
			return responses[commandKey](command)
		else:
			print "Command '%s' not found" % command
	except Exception, e:
		print e
		
	return 'ok\n'

def setExtruderTemperature(command):
	try:
		#M104 S210 or M109 S[temp]
		sIndex = command.find('S') + 1
		global extruderGoalTemperature
		extruderGoalTemperature = int(command[sIndex:])
	except Exception, e:
		print e
		
	return 'ok\n'

def setBedTemperature(command):
	try:
		#M140 S210 or M190 S[temp]
		sIndex = command.find('S') + 1
		global bedGoalTemperature
		bedGoalTemperature = int(command[sIndex:])
	except Exception, e:
		print e
		
	return 'ok\n'

"""Dictionary of command and response callback"""
responses = { "M105" : randomTemp, "A" : echo, "M114" : getPosition , "N" : parseChecksumLine, "M115" : reportMarlinFirmware, "M104" : setExtruderTemperature, "M109" : setExtruderTemperature, "M140" : setBedTemperature,"M190" : setBedTemperature }

def main(argv):
	parser = argparse.ArgumentParser(description='Set up a printer emulation.')

	if len(argv) > 0:
		ser = serial.Serial(argv[0], 250000, timeout=1)	
	else:
		ser = serial.Serial('COM14', 250000, timeout=1)	

	run_slow = len(argv) > 1 and argv[1] == 'slow'

	waitForKey = True

	print '\n Initializing emulator (Speed: %s)' % ('slow' if run_slow else 'fast')
	while True:
		line = ser.readline()   # read a '\n' terminated line
		if len(line) > 0:
			print(line)
			response = getCorrectResponse(line)

			if run_slow:
				sleep(0.02)

			print response
			ser.write(response)
	ser.close()

if __name__ == "__main__":
	main(sys.argv[1:])
