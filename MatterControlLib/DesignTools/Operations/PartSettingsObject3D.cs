/*
Copyright (c) 2022, Lars Brubaker
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

using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    [HideChildrenFromTreeView]
    [RequiresPermissions]
    [HideMeterialAndColor]
    public class PartSettingsObject3D : Object3D, IEditorButtonProvider, IStaticThumbnail
    {
        public PartSettingsObject3D()
        {
            Name = "Part Settings".Localize();
        }

        public PrinterSettingsLayer Overrides { get; set; } = new PrinterSettingsLayer();

        public static async Task<PartSettingsObject3D> Create()
        {
            var item = new PartSettingsObject3D();
            await item.Rebuild();
            return item;
        }

        private static object loadLock = new object();
        private static IObject3D sliceSettingsObject;

        public override Mesh Mesh
        {
            get
            {
                if (this.Children.Count == 0)
                {
                    lock (loadLock)
                    {
                        if (sliceSettingsObject == null)
                        {
                            sliceSettingsObject = MeshContentProvider.LoadMCX(StaticData.Instance.OpenStream(Path.Combine("Stls", "part_settings.mcx")));
                        }

                        this.Children.Modify((list) =>
                        {
                            list.Clear();

                            list.Add(sliceSettingsObject.Clone());
                        });
                    }
                }

                return null;
            }

            set => base.Mesh = value;
        }

        public override bool Printable => false;

        public string ThumbnailName => nameof(PartSettingsObject3D);

        private void UpdateSettingsDisplay(PrinterConfig containingPrinter)
        {
            if (containingPrinter != null)
            {
                this.Invalidate(InvalidateType.DisplayValues);
                ApplicationController.Instance.ReloadSettings(containingPrinter);
                // refresh the properties pannel by unselecting and selecting
                containingPrinter.Bed.Scene.SelectedItem = null;
                containingPrinter.Bed.Scene.SelectedItem = this;
            }
        }

        public static HashSet<string> settingsToIgnore = new HashSet<string>()
        {
            SettingsKey.spiral_vase,
            SettingsKey.layer_to_pause,
            SettingsKey.perimeter_acceleration,
            SettingsKey.default_acceleration,
            SettingsKey.t1_extrusion_move_speed_multiplier,
            SettingsKey.bed_surface,
            SettingsKey.brim_extruder,
            SettingsKey.support_material_extruder,
            SettingsKey.support_material_interface_extruder,
            SettingsKey.material_color,
            SettingsKey.material_color_1,
            SettingsKey.material_color_2,
            SettingsKey.material_color_3,
            SettingsKey.filament_diameter,
            SettingsKey.filament_density,
            SettingsKey.filament_cost,
            SettingsKey.temperature,
            SettingsKey.temperature1,
            SettingsKey.temperature2,
            SettingsKey.temperature3,
            SettingsKey.bed_temperature,
            SettingsKey.bed_temperature_blue_tape,
            SettingsKey.bed_temperature_buildtak,
            SettingsKey.bed_temperature_garolite,
            SettingsKey.bed_temperature_glass,
            SettingsKey.bed_temperature_kapton,
            SettingsKey.bed_temperature_pei,
            SettingsKey.bed_temperature_pp,
            SettingsKey.inactive_cool_down,
            SettingsKey.seconds_to_reheat,
        };

        public IEnumerable<EditorButtonData> GetEditorButtonsData()
        {
            var containingPrinter = this.ContainingPrinter();
            if (containingPrinter != null)
            {
                yield return new EditorButtonData()
                {
                    Action = () =>
                    {
                        var settingsContext = new SettingsContext(containingPrinter, null, NamedSettingsLayers.All);
                        foreach (var setting in containingPrinter.Settings.UserLayer)
                        {
                            var data = SliceSettingsRow.GetStyleData(containingPrinter, ApplicationController.Instance.Theme, settingsContext, setting.Key, true);

                            if (!settingsToIgnore.Contains(setting.Key) && data.showRestoreButton)
                            {
                                Overrides[setting.Key] = setting.Value;
                            }
                        }
                        UpdateSettingsDisplay(containingPrinter);
                    },
                    Name = "Add User Overrides".Localize(),
                    HelpText = "Copy in all current user overides".Localize()
                };
            }


            if (ApplicationController.Instance.UserHasPermission(this))
            {
                yield return new EditorButtonData()
                {
                    Action = () =>
                    {
                        var settings = new PrinterSettings();
                        settings.GetSceneLayer = () => Overrides;
                        var printer = new PrinterConfig(settings);
                        if (containingPrinter != null)
                        {
                            printer = containingPrinter;
                        }

                        // set this after the PrinterConfig is constructed to change it to overrides
                        // set this after the PrinterConfig is constructed to change it to overrides
                        settings.GetSceneLayer = () => Overrides;

                        var presetsContext = new PresetsContext(null, printer.Settings.SceneLayer)
                        {
                            LayerType = NamedSettingsLayers.Scene,
                        };

                        var editMaterialPresetsPage = new SlicePresetsPage(printer, presetsContext, false);
                        editMaterialPresetsPage.Closed += (s, e2) =>
                        {
                            ApplicationController.Instance.AcitveSlicePresetsPage = null;
                            UpdateSettingsDisplay(containingPrinter);
                        };

                        ApplicationController.Instance.AcitveSlicePresetsPage = editMaterialPresetsPage;
                        DialogWindow.Show(editMaterialPresetsPage);
                    },
                    Name = "Edit".Localize(),
                };
            }
        }
    }
}