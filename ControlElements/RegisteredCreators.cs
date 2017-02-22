﻿/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.CreatorPlugins
{
	public class CreatorInformation
	{
		public delegate void UnlockFunction();

		public UnlockFunction unlockFunction;

		public delegate bool PermissionFunction();

		public PermissionFunction permissionFunction;

		public delegate void UnlockRegisterFunction(EventHandler functionToCallOnEvent, ref EventHandler functionThatWillBeCalledToUnregisterEvent);

		public UnlockRegisterFunction unlockRegisterFunction;
		public Action Show;
		public string iconPath;
		public string description;
		public bool paidAddOnFlag;

		public CreatorInformation(
			Action showFunction,
			string iconPath, string description,
			bool paidAddOnFlag = false,
			UnlockFunction unlockFunction = null,
			PermissionFunction permissionFunction = null,
			UnlockRegisterFunction unlockRegisterFunction = null)
		{
			this.Show = showFunction;
			this.iconPath = iconPath;
			this.description = description;
			this.paidAddOnFlag = paidAddOnFlag;
			this.unlockFunction = unlockFunction;
			this.permissionFunction = permissionFunction;
			this.unlockRegisterFunction = unlockRegisterFunction;
		}
	}

	public class RegisteredCreators
	{
		private static RegisteredCreators instance = null;

		public static RegisteredCreators Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new RegisteredCreators();
				}

				return instance;
			}
		}

		public List<CreatorInformation> Creators = new List<CreatorInformation>();

		private RegisteredCreators()
		{
		}

		public void RegisterLaunchFunction(CreatorInformation creatorInformation)
		{
			Creators.Add(creatorInformation);
		}
	}
}