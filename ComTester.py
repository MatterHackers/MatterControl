import serial

ser = serial.Serial('COM14', 250000, timeout=1)
x = ser.read()          # read one byte
s = ser.read(10)        # read up to ten bytes (timeout)
while True:
	line = ser.readline()   # read a '\n' terminated line
	print line
	ser.write('ok\n')
ser.close()