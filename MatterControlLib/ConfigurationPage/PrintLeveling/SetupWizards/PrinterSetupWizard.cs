/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using System.Collections;
using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public abstract class PrinterSetupWizard : ISetupWizard
	{
		private IEnumerator<WizardPage> pages;
		protected PrinterConfig printer;

		public PrinterSetupWizard(PrinterConfig printer)
		{
			this.printer = printer;
		}

		protected abstract IEnumerator<WizardPage> GetPages();

		public abstract bool SetupRequired { get; }

		public abstract bool Visible { get; }

		public virtual string HelpText { get; }

		public virtual bool Completed => !this.SetupRequired;

		public abstract bool Enabled { get; }

		public string Title { get; protected set; }

		public PrinterConfig Printer => printer;

		public WizardPage Current
		{
			get
			{
				if (pages == null)
				{
					// Reset enumerator, move to first item
					this.Reset();
					this.MoveNext();
				}

				return pages.Current;
			}
		}

		object IEnumerator.Current => pages.Current;

		public Vector2 WindowSize { get; protected set; }

		public bool RequireCancelConfirmation { get; protected set; }

		public virtual bool ClosePage()
		{
			return true;
		}

		public bool MoveNext()
		{
			// Shutdown active page
			pages.Current?.Close();

			// Advance
			return pages.MoveNext();
		}

		public void Reset()
		{
			pages = this.GetPages();
		}

		public virtual void Dispose()
		{
		}
	}
}