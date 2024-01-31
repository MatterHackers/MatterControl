/*
Copyright (c) 2023, John Lewin, Lars Brubaker
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Matter_CAD_Lib.DesignTools.Objects3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using static MatterHackers.VectorMath.Easing;

namespace MatterHackers.DataConverters3D
{
    public class InteractiveScene : IObject3D
	{
		public event EventHandler SelectionChanged;

		public event EventHandler<InvalidateArgs> Invalidated;

		private IObject3D _selectedItem;

		public InteractiveScene()
		{
			SourceItem = new Object3D();
		}

		public virtual Task Rebuild()
		{
			return Task.CompletedTask;
		}

		[JsonIgnore]
		public IObject3D SelectedItemRoot { get; private set; }

		[JsonIgnore]
		public IObject3D SelectedItem
		{
			get => _selectedItem;
			set
			{
				if (_selectedItem != value)
				{
					if (_selectedItem is SelectionGroupObject3D)
					{
						// If the selected item is a SelectionGroup, collapse its contents into the root
						// of the scene when it loses focus
						Children.Modify(list =>
						{
							_selectedItem.CollapseInto(list);
						});
					}

					_selectedItem = value;

					// When setting SelectedItem, SelecteItemRoot is either the SelectedItem if rooted, or the first ancestor in the root of the scene
					if (_selectedItem?.Parent.Parent != null)
					{
						this.SelectedItemRoot = _selectedItem.Parents().FirstOrDefault(o => o.Parent.Parent == null);
					}
					else
					{
						this.SelectedItemRoot = _selectedItem;
					}

					SelectionChanged?.Invoke(this, null);
				}
			}
		}

		[JsonIgnore]
		public UndoBuffer UndoBuffer { get; } = new UndoBuffer();

		[JsonIgnore]
		public bool ShowSelectionShadow { get; set; } = true;

		public async void Save(Stream stream, Action<double, string> progress = null)
		{
			// Serialize the scene to disk using a modified Json.net pipeline with custom ContractResolvers and JsonConverters
			try
			{
				using (var streamWriter = new StreamWriter(stream))
				{
					streamWriter.Write(await this.ToJson());
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error saving file: ", ex.Message);
			}
		}

		public void Save(string mcxPath, Action<double, string> progress = null)
		{
			using (var stream = new FileStream(mcxPath, FileMode.Create, FileAccess.Write))
			{
				this.Save(stream, progress);
			}
		}

		[JsonIgnore]
		// TODO: Remove from InteractiveScene - coordinate debug details between MeshViewer and Inspector directly
		public IObject3D DebugItem { get; set; }

		public void SelectLastChild()
		{
			if (Children.Count > 0)
			{
				SelectedItem = Children.Last();
			}
		}

		public void SelectFirstChild()
		{
			if (Children.Count > 0)
			{
				SelectedItem = Children.First();
			}
		}

		public void ClearSelection()
		{
			SelectedItem = null;
		}

		public void SetSelection(IEnumerable<IObject3D> items)
		{
			if (items.Count() == 1)
			{
				// Add a single item directly as the SelectedItem
				SelectedItem = items.First();
			}
			else
			{
				// Add a range of items wrapped with a new SelectionGroup
				var SelectionGroup = new SelectionGroupObject3D(items)
				{
					Name = "Selection".Localize()
				};

				// Move selected items into a new SelectionGroup
				_sourceItem.Children.Modify(list =>
				{
					foreach (var item in items)
					{
						list.Remove(item);
					}

					// Add the SeletionGroup as the first child
					list.Insert(0, SelectionGroup);
				});

				SelectedItem = SelectionGroup;
			}
		}

		public void AddToSelection(IObject3D itemToAdd)
		{
			var selectedItem = SelectedItem;
			if (itemToAdd == selectedItem || selectedItem?.Children?.Contains(itemToAdd) == true)
			{
				return;
			}

			if (selectedItem != null)
			{
				if (selectedItem is SelectionGroupObject3D)
				{
					// Remove from the scene root
					this.Children.Modify(list => list.Remove(itemToAdd));

					// Move into the SelectionGroup
					selectedItem.Children.Modify(list => list.Add(itemToAdd));
				}
				else // add a new selection group and add to its children
				{
					// We're adding a new item to the selection. To do so we wrap the selected item
					// in a new group and with the new item. The selection will continue to grow in this
					// way until it's applied, due to a loss of focus or until a group operation occurs
					var newSelectionGroup = new SelectionGroupObject3D(new[] { selectedItem, itemToAdd })
					{
						Name = "Selection".Localize()
					};

					this.Children.Modify(list =>
					{
						list.Remove(itemToAdd);
						list.Remove(selectedItem);
						// add the seletionGroup as the first item so we can hit it first
						list.Insert(0, newSelectionGroup);
					});

					SelectedItem = newSelectionGroup;
				}
			}
			else if (Children.Contains(itemToAdd))
			{
				SelectedItem = itemToAdd;
			}
			else
			{
				throw new Exception("Unable to select external object. Item must be in the scene to be selected.");
			}
		}

		public void Load(IObject3D sourceItem)
		{
			this.SourceItem = sourceItem;
		}

		IObject3D _sourceItem  = null;

        private void ValidateSceneStructure()
		{
            void ValidateAllChildrenHaveParentSetToParent(IObject3D parent)
            {
                foreach (var child in parent.Children)
                {
                    if (!child.RebuildLocked
                        && child != _sourceItem
						&& child.Parent != parent
						&& child.Parent.GetType() != typeof(SelectionGroupObject3D))
                    {
                        //throw new Exception("Child does not have parent set to parent.");
                    }

                    ValidateAllChildrenHaveParentSetToParent(child);
                }
            }

            // Check that every childs parent is this recusively
            ValidateAllChildrenHaveParentSetToParent(_sourceItem);
        }


        private IObject3D SourceItem
		{
			get
			{
				if (_sourceItem != null 
					&& _sourceItem.Matrix != Matrix4X4.Identity)
                {
					_sourceItem.Matrix = Matrix4X4.Identity;
				}

#if DEBUG
				ValidateSceneStructure();
#endif

				return _sourceItem;
			}
			set
			{
				void SourceItem_Invalidated(object sender, InvalidateArgs e)
				{
					Invalidate(e);
				}

				if (_sourceItem != null)
				{
					_sourceItem.Children.ItemsModified -= OnItemsModified;
					_sourceItem.Invalidated -= SourceItem_Invalidated;
				}

				_sourceItem = value;

				_sourceItem.Invalidated += SourceItem_Invalidated;
				_sourceItem.Children.ItemsModified += OnItemsModified;

				// and call it
				OnItemsModified(this, null);
			}
		}

		private void OnItemsModified(object sender, EventArgs e)
		{
			ItemsModified?.Invoke(_sourceItem, null);
		}

		public string OwnerID { get => SourceItem.OwnerID; set => SourceItem.OwnerID = value; }

		public string CloneID { get => SourceItem.CloneID; set => SourceItem.CloneID = value; }

		public AscendableSafeList<IObject3D> Children { get => SourceItem.Children; set => SourceItem.Children = value; }

		public IObject3D Parent { get => SourceItem.Parent; set => SourceItem.Parent = value; }

		[JsonIgnore]
		public Color Color { get => SourceItem.Color; set => SourceItem.Color = value; }

		public bool Contains(IObject3D item)
		{
			if (item?.Parent == null)
            {
				return false;
            }

			// validate that every parent has as a child the item that had it as a parent
			var child = item;
			var parent = item.Parent;
			while (parent != null)
			{
				if (!parent.Children.Contains(child))
				{
					return false;
				}

				child = parent;
				parent = parent.Parent;
			}

			if (child != SourceItem)
			{
				// the last child is not our scene root item
				return false;
			}

			return true;
		}

		[JsonIgnore]
		public PrintOutputTypes OutputType { get => SourceItem.OutputType; set => SourceItem.OutputType = value; }

		[JsonIgnore]
		public Matrix4X4 Matrix { get => SourceItem.Matrix; set => SourceItem.Matrix = value; }

		[JsonIgnore]
		public string TypeName => SourceItem.TypeName;

		[JsonIgnore]
		public IVertexSource VertexSource
        {
			get;
			set;
        }
        
		[JsonIgnore]
		public Mesh Mesh { get => SourceItem.Mesh; set => SourceItem.Mesh = value; }

		[JsonIgnore]
		public string MeshPath { get => SourceItem.MeshPath; set => SourceItem.MeshPath = value; }

		[JsonIgnore]
		public string Name { get => SourceItem.Name; set => SourceItem.Name = value; }

		[JsonIgnore]
		public bool Persistable => SourceItem.Persistable;

		[JsonIgnore]
		public bool Visible { get => SourceItem.Visible; set => SourceItem.Visible = value; }

		public string ID { get => SourceItem.ID; set => SourceItem.ID = value; }

		[JsonIgnore]
		public bool CanEdit => false;

		[JsonIgnore]
		public bool CanApply => false;

		[JsonIgnore]
		public bool CanRemove => false;

		[JsonIgnore]
		public bool DrawSelection { get; set; } = true;

		[JsonIgnore]
		public bool RebuildLocked => false;

		public bool Expanded => true;

		private ulong lastSaveUndoHash = 0;

		public void MarkSavePoint()
		{
			lastSaveUndoHash = UndoBuffer.GetLongHashCode();
		}

        [JsonIgnore]
		public bool HasUnsavedChanges
		{
			get
            {
				if (lastSaveUndoHash == UndoBuffer.GetLongHashCode())
                {
					return false;
                }

				return true;
			}
		}

        public int CloneUpdateCount { get => SourceItem.CloneUpdateCount; set => SourceItem.CloneUpdateCount = value; }

        public event EventHandler ItemsModified;

		public IObject3D DeepCopy() => SourceItem.DeepCopy();

		public async Task<string> ToJson(Action<double, string> progress = null)
		{
			try
			{
				await this.PersistAssets(progress);

				// Clear the selection before saving
				var selectedItems = new List<IObject3D>();

				if (this.SelectedItem != null)
				{
					if (this.SelectedItem is SelectionGroupObject3D selectionGroup)
					{
						foreach (var item in selectionGroup.Children)
						{
							selectedItems.Add(item);
						}
					}
					else
					{
						selectedItems.Add(this.SelectedItem);
					}
				}

				var json = await SourceItem.ToJson();

				// Restore the selection after saving
				foreach (var item in selectedItems)
				{
					this.AddToSelection(item);
				}

				return json;
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error converting to json: ", ex.Message);
			}

			return "";
		}

		public ulong GetLongHashCode(ulong hash = 14695981039346656037) => SourceItem.GetLongHashCode(hash);

		public ITraceable GetBVHData() => SourceItem.GetBVHData();

		public void SetMeshDirect(Mesh mesh) => SourceItem.SetMeshDirect(mesh);

		public HashSet<IObject3D> clonesNeedingUpdate = new HashSet<IObject3D>();
		public void ClonesNeedUpdate(IObject3D updatedClone)
		{
			lock(clonesNeedingUpdate)
			{
                if (clonesNeedingUpdate.Count == 0)
                {
                    UiThread.RunOnIdle(CopyClones);
                }

                clonesNeedingUpdate.Add(updatedClone);
            }
        }

        private void CopyClones()
        {
            lock (clonesNeedingUpdate)
            {
                foreach (var sourceClone in clonesNeedingUpdate)
                {
                    var clones = this.DescendantsAndSelf().Where(i => i.CloneID == sourceClone.CloneID && i != sourceClone).ToList();
                    if (clones.Count > 0)
                    {
                        foreach (var existingClone in clones)
                        {
							var cloneCopy = sourceClone.DeepCopy();
                            var existingCloneParent = existingClone.Parent;
							using (existingCloneParent.RebuildLock())
							{
								existingCloneParent.Children.Modify(existingCloneParentChildren =>
								{
									var exisitngCloneIndex = existingCloneParentChildren.IndexOf(existingClone);
									// replace the existing clone with the new clone
									existingCloneParentChildren[exisitngCloneIndex] = cloneCopy;
								});
							}

							// wait until the next update to invalidate the parents
							UiThread.RunOnIdle(() => existingCloneParent.Invalidate(new InvalidateArgs(cloneCopy, InvalidateType.Children)));
                        }
                    }
                    else
                    {
                        // If no clones were found, the source is not a clone, so we can remove the clone ID
                        sourceClone.CloneID = null;
                    }

					// set all the clone counts to 0
					foreach(var clone in clones)
					{
						clone.CloneUpdateCount = 0;
					}
                }

				this.Invalidate(new InvalidateArgs(this, InvalidateType.DisplayValues));

                clonesNeedingUpdate.Clear();
			}
        }

        public void Invalidate(InvalidateArgs invalidateType)
		{
			this.Invalidated?.Invoke(this, invalidateType);
        }

        public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			return SourceItem.GetAxisAlignedBoundingBox(matrix);
		}

		public void Apply(UndoBuffer undoBuffer)
		{
			throw new NotImplementedException();
		}

		public void Cancel(UndoBuffer undoBuffer)
		{
			throw new NotImplementedException();
		}

		public RebuildLock RebuildLock()
		{
			throw new NotImplementedException();
		}

		public void Undo()
		{
			using (new SelectionMaintainer(this))
			{
				UndoBuffer.Undo();
			}
		}

		public void Redo()
		{
			using (new SelectionMaintainer(this))
			{
				UndoBuffer.Redo();
			}
		}

		public bool Equals(IObject3D other)
		{
			return base.Equals(other);
		}

		public List<IObject3D> GetSelectedItems()
		{
			var selectedItem = this.SelectedItem;
			var selectedItems = new List<IObject3D>();
			if (selectedItem != null)
			{
				if (selectedItem is SelectionGroupObject3D)
				{
					selectedItems = selectedItem.Children.ToList();
				}
				else
				{
					selectedItems = new List<IObject3D> { selectedItem };
				}
			}

			return selectedItems;
		}
    }
}
