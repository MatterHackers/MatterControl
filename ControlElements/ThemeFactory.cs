/*
Copyright (c) 2014, Kevin Pope
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
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;

namespace MatterHackers.MatterControl
{
    public class ActiveTheme
    {
        static ActiveTheme globalInstance;
        private Theme loadedTheme;
        private List<Theme> availableThemes;
        private int defaultThemeIndex = 1;
        private int activeThemeIndex = -1;

        public RootedObjectEventHandler ThemeChanged = new RootedObjectEventHandler();

        public enum ApplicationDisplayType { Responsive, Touchscreen } ;

        public ApplicationDisplayType DisplayMode
        {
            get
            {
                string displayMode = UserSettings.Instance.get("ApplicationDisplayMode");
                if (displayMode == "touchscreen")
                {
                    return ApplicationDisplayType.Touchscreen;
                }
                else
                {
                    return ApplicationDisplayType.Responsive;
                }
            }
        }

        public List<Theme> AvailableThemes
        {
            get
            {
                if (this.availableThemes == null)
                {
                    this.availableThemes = GetAvailableThemes();
                }
                return availableThemes;
            }
        }

        public bool IsDarkTheme
        {
            get
            {
                return loadedTheme.DarkTheme;
            }
        }

        public RGBA_Bytes TransparentDarkOverlay
        {
            get
            {
                return new RGBA_Bytes(0,0,0,50);
            }
        }

        public RGBA_Bytes TransparentLightOverlay
        {
            get
            {
                return new RGBA_Bytes(255,255,255,50);
            }
        }


        public RGBA_Bytes TabLabelSelected
        {
            get
            {
                return loadedTheme.tabLabelSelectedColor;
            }
        }

        public RGBA_Bytes TabLabelUnselected
        {
            get
            {
                return loadedTheme.tabLabelUnselectedColor;
            }
        }

        public RGBA_Bytes SecondaryTextColor
        {
            get
            {
                return loadedTheme.secondaryTextColor;
            }
        }

        public RGBA_Bytes PrimaryBackgroundColor
        {
            get
            {
                return loadedTheme.primaryBackgroundColor;
            }
        }

        public RGBA_Bytes SecondaryBackgroundColor
        {
            get
            {
                return loadedTheme.secondaryBackgroundColor;
            }
        }

        public RGBA_Bytes TertiaryBackgroundColor
        {
            get
            {
                return loadedTheme.tertiaryBackgroundColor;
            }
        }

        public RGBA_Bytes PrimaryTextColor
        {
            get
            {
                return loadedTheme.primaryTextColor;
            }
        }

        public RGBA_Bytes PrimaryAccentColor
        {
            get
            {
                return loadedTheme.primaryAccentColor;
            }
        }


        public RGBA_Bytes SecondaryAccentColor
        {
            get
            {
                return loadedTheme.secondaryAccentColor;
            }
        }

        private void OnThemeChanged(EventArgs e)
        {
            ThemeChanged.CallEvents(this, e);
        }

        public ActiveTheme()
        {
            //Load the default theme by index
            if (UserSettings.Instance.get("ActiveThemeIndex") == null)
            {
                bool foundOemColor = false;
                for (int i = 0; i < AvailableThemes.Count; i++)
                {
                    Theme current = AvailableThemes[i];
                    if (current.Name == OemSettings.Instance.ThemeColor)
                    {
                        UserSettings.Instance.set("ActiveThemeIndex", i.ToString());
                        foundOemColor = true;
                        break;
                    }
                }

                if (!foundOemColor)
                {
                    UserSettings.Instance.set("ActiveThemeIndex", defaultThemeIndex.ToString());
                }
            }

            int themeIndex;
            try
            {
                themeIndex = Convert.ToInt32(UserSettings.Instance.get("ActiveThemeIndex"));
            }
            catch
            {
                themeIndex = defaultThemeIndex;
            }

            LoadThemeSettings(themeIndex);
        }

        public static ActiveTheme Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new ActiveTheme();
                }
                return globalInstance;
            }
        }

        public void LoadThemeSettings(int index)
        {
            //Validate new theme selection and change theme
            if (index > -1 && index < AvailableThemes.Count)
            {
                if (activeThemeIndex != index)
                {
                    this.loadedTheme = this.AvailableThemes[index];
                    this.activeThemeIndex = index;
                    OnThemeChanged(null);
                }
            }
            else
            {
                throw new Exception("Invalid theme selection");
            }
        }


        private List<Theme> GetAvailableThemes()
        {
            //Generate a list of available theme definitions
            List<Theme> themeList = new List<Theme>();

            //Dark themes
            themeList.Add(new Theme("Blue - Dark", new RGBA_Bytes(0, 75, 139), new RGBA_Bytes(0, 103, 190)));
            themeList.Add(new Theme("Teal - Dark", new RGBA_Bytes(0, 130, 153), new RGBA_Bytes(0, 173, 204)));
            themeList.Add(new Theme("Green - Dark", new RGBA_Bytes(0, 138, 23), new RGBA_Bytes(0, 189, 32)));
            themeList.Add(new Theme("Light Blue - Dark", new RGBA_Bytes(93, 178, 255), new RGBA_Bytes(144, 202, 255)));
            themeList.Add(new Theme("Orange - Dark", new RGBA_Bytes(255, 129, 25), new RGBA_Bytes(255, 157, 76)));
            themeList.Add(new Theme("Purple - Dark", new RGBA_Bytes(70, 23, 180), new RGBA_Bytes(104, 51, 229)));
            themeList.Add(new Theme("Red - Dark", new RGBA_Bytes(172, 25, 61), new RGBA_Bytes(217, 31, 77)));
            themeList.Add(new Theme("Pink - Dark", new RGBA_Bytes(220, 79, 173), new RGBA_Bytes(233, 143, 203)));
            themeList.Add(new Theme("Grey - Dark", new RGBA_Bytes(88, 88, 88), new RGBA_Bytes(114, 114, 114)));
            themeList.Add(new Theme("Pink - Dark", new RGBA_Bytes(140, 0, 149), new RGBA_Bytes(188, 0, 200)));

            //Light themes
            themeList.Add(new Theme("Blue - Light", new RGBA_Bytes(0, 75, 139), new RGBA_Bytes(0, 103, 190), false));
            themeList.Add(new Theme("Teal - Light", new RGBA_Bytes(0, 130, 153), new RGBA_Bytes(0, 173, 204), false));
            themeList.Add(new Theme("Green - Light", new RGBA_Bytes(0, 138, 23), new RGBA_Bytes(0, 189, 32), false));
            themeList.Add(new Theme("Light Blue - Light", new RGBA_Bytes(93, 178, 255), new RGBA_Bytes(144, 202, 255), false));
            themeList.Add(new Theme("Orange - Light", new RGBA_Bytes(255, 129, 25), new RGBA_Bytes(255, 157, 76), false));
            themeList.Add(new Theme("Purple - Light", new RGBA_Bytes(70, 23, 180), new RGBA_Bytes(104, 51, 229), false));
            themeList.Add(new Theme("Red - Light", new RGBA_Bytes(172, 25, 61), new RGBA_Bytes(217, 31, 77), false));
            themeList.Add(new Theme("Pink - Light", new RGBA_Bytes(220, 79, 173), new RGBA_Bytes(233, 143, 203), false));
            themeList.Add(new Theme("Grey - Light", new RGBA_Bytes(88, 88, 88), new RGBA_Bytes(114, 114, 114), false));
            themeList.Add(new Theme("Pink - Light", new RGBA_Bytes(140, 0, 149), new RGBA_Bytes(188, 0, 200), false));

			return themeList;
        }
    }

    public class Theme
    {
        public RGBA_Bytes primaryAccentColor;
        public RGBA_Bytes secondaryAccentColor;
        public RGBA_Bytes primaryTextColor;
        public RGBA_Bytes secondaryTextColor;
        public RGBA_Bytes primaryBackgroundColor;
        public RGBA_Bytes secondaryBackgroundColor;
        public RGBA_Bytes tabLabelSelectedColor;
        public RGBA_Bytes tabLabelUnselectedColor;

        public RGBA_Bytes tertiaryBackgroundColor;
        public RGBA_Bytes tertiaryBackgroundColorDisabled;

        string name;
        public string Name { get { return name; } }
        bool darkTheme;

        public bool DarkTheme { get { return darkTheme; } }

        public Theme(string name, RGBA_Bytes primary, RGBA_Bytes secondary, bool darkTheme = true)
        {
            this.darkTheme = darkTheme;
            this.name = name;

            if (darkTheme)
            {
                this.primaryAccentColor = primary;
                this.secondaryAccentColor = secondary;
                
                this.primaryBackgroundColor = new RGBA_Bytes(68, 68, 68);
                this.secondaryBackgroundColor = new RGBA_Bytes(51, 51, 51);
                
                this.tabLabelSelectedColor = new RGBA_Bytes(255, 255, 255);
                this.tabLabelUnselectedColor = new RGBA_Bytes(180, 180, 180);
                this.primaryTextColor = new RGBA_Bytes(255, 255, 255);
                this.secondaryTextColor = new RGBA_Bytes(200, 200, 200);

                this.tertiaryBackgroundColor = new RGBA_Bytes(62, 62, 62);
            }
            else
            {
                this.primaryAccentColor = secondary;
                this.secondaryAccentColor = primary;

                this.primaryBackgroundColor = new RGBA_Bytes(208, 208, 208);
                this.secondaryBackgroundColor = new RGBA_Bytes(185, 185, 185);
                this.tabLabelSelectedColor = new RGBA_Bytes(51, 51, 51);
                this.tabLabelUnselectedColor = new RGBA_Bytes(102, 102, 102);
                this.primaryTextColor = new RGBA_Bytes(34, 34, 34);
                this.secondaryTextColor = new RGBA_Bytes(51, 51, 51);

                this.tertiaryBackgroundColor = new RGBA_Bytes(190, 190, 190);
            }
        }
    }
}


