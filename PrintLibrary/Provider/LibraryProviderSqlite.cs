/*
Copyright (c) 2015, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MatterHackers.MatterControl.PrintLibrary.Provider
{
	public class LibraryProviderSQLite : LibraryProvider
	{
		private string parentKey = null;

		public LibraryProviderSQLite(string parentKey)
		{
			this.parentKey = parentKey;
		}

		public override int CollectionCount
		{
			get
			{
				return 0;
			}
		}

		public override bool HasParent
		{
			get
			{
				if (parentKey != null)
				{
					return true;
				}

				return false;
			}
		}

		public override int ItemCount
		{
			get
			{
				return LibrarySQLiteData.Instance.Count;
			}
		}

		public override string ProviderTypeKey
		{
			get
			{
				return "LibraryProviderSqliteKey";
			}
		}

		public override string KeywordFilter
		{
			get
			{
				return LibrarySQLiteData.Instance.KeywordFilter;
			}

			set
			{
				LibrarySQLiteData.Instance.KeywordFilter = value;
			}
		}

		public override string Name
		{
			get
			{
				return "Local Library";
			}
		}

		public override void AddCollectionToLibrary(string collectionName)
		{
			throw new NotImplementedException();
		}

		public override void AddFilesToLibrary(IList<string> files, ReportProgressRatio reportProgress = null, RunWorkerCompletedEventHandler callback = null)
		{
			LibrarySQLiteData.Instance.LoadFilesIntoLibrary(files, reportProgress, callback);
		}

		public override PrintItemCollection GetCollectionItem(int collectionIndex)
		{
			throw new NotImplementedException();
		}

		public override PrintItemCollection GetParentCollectionItem()
		{
			if (parentKey != null)
			{
				return new PrintItemCollection("..", parentKey);
			}
			else
			{
				return null;
			}
		}

		public override PrintItemWrapper GetPrintItemWrapper(int itemIndex)
		{
			return LibrarySQLiteData.Instance.GetPrintItemWrapper(itemIndex);
		}

		public override void RemoveCollection(string collectionName)
		{
			throw new NotImplementedException();
		}

		public override void RemoveItem(PrintItemWrapper printItemWrapper)
		{
			LibrarySQLiteData.Instance.RemoveItem(printItemWrapper);
		}

		public override void SetCollectionBase(PrintItemCollection collectionBase)
		{
		}
	}
}