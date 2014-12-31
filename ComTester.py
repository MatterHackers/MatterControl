import serial
import time
import random

ser = serial.Serial('COM14', 250000, timeout=1)
print "Initializing emulator..."

"""Add response callbacks here"""
def randomTemp(command):
	return "ok T:%s\n" % random.randrange(202,215)

def echo(command):
	return command

"""Dictionary of command and response callback"""
responses = { "M105" : randomTemp, "A": echo}

def getCorrectResponse(command):
	try:
		#Remove line returns
		command = ''.join(command.splitlines())
		if command in responses:			
			return responses[command](command)
	except Exception, e:
		print e
		
	return 'ok\n'

while True:
	line = ser.readline()   # read a '\n' terminated line
	if len(line) > 0:
		print(line)
		response = getCorrectResponse(line)
		print response
		ser.write(response)
ser.close()