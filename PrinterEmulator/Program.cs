// Copyright (c) 2015, Lars Brubaker
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met: 
// 
// 1. Redistributions of source code must retain the above copyright notice, this
// list of conditions and the following disclaimer. 
// 2. Redistributions in binary form must reproduce the above copyright notice,
// this list of conditions and the following disclaimer in the documentation
// and/or other materials provided with the distribution. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
// ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
// This is to test connection and printing. We use it with com0com 
// to validate MatterControl under various situations.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrinterEmulator
{
	class Program
	{
		static void Main(string[] args)
		{
		/*
			parser = argparse.ArgumentParser(description = 'Set up a printer emulation.')

	if len(argv) > 0:
		ser = serial.Serial(argv[0], 250000, timeout = 1)
	else:
		ser = serial.Serial('COM14', 250000, timeout = 1)

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
	*/
		}
	}
}
