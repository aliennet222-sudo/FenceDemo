using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace FenceDemo
{
    // -------------------------------
    // Fenster-Einstellungen speichern
    // -------------------------------
    public class WindowSettings
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    // -------------------------------
    // Datei-Eintrag für die ListBox
    // -------------------------------
    public class DateiEintrag
    {
        public ImageSource Icon { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }

    // -------------------------------
    // Shell API für Datei-Icons
    // -------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    public static class ShellIcon
    {
        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_SMALLICON = 0x1;
    }

    // -------------------------------
    // MainWindow
    // -------------------------------
    public partial class MainWindow : Window
    {
        private string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FenceDemo",
            "windowsettings.json");

        private string fileListPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FenceDemo",
            "filelist.json");

        private List<string> gespeicherteDateien = new List<string>();

        private Point dragStartPoint;
        private DateiEintrag draggedItem;
        private bool isDraggingOutside = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadWindowSettings();
            LoadFileList();
            UpdateFileListUI();
        }

        // -------------------------------
        // Fensterposition speichern
        // -------------------------------
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowSettings();
            base.OnClosing(e);
        }

        private void LoadWindowSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var s = JsonSerializer.Deserialize<WindowSettings>(json);

                    this.Left = s.Left;
                    this.Top = s.Top;
                    this.Width = s.Width;
                    this.Height = s.Height;
                }
            }
            catch { }
        }

        private void SaveWindowSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));

                var s = new WindowSettings()
                {
                    Left = this.Left,
                    Top = this.Top,
                    Width = this.Width,
                    Height = this.Height
                };

                var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch { }
        }

        // -------------------------------
        // Datei-Liste speichern
        // -------------------------------
        private void LoadFileList()
        {
            try
            {
                if (File.Exists(fileListPath))
                {
                    var json = File.ReadAllText(fileListPath);
                    gespeicherteDateien = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch
            {
                gespeicherteDateien = new List<string>();
            }
        }

        private void SaveFileList()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fileListPath));
                File.WriteAllText(fileListPath, JsonSerializer.Serialize(gespeicherteDateien));
            }
            catch { }
        }

        // -------------------------------
        // Datei-Icon holen (Shell API)
        // -------------------------------
        private ImageSource GetFileIcon(string filePath)
        {
            SHFILEINFO shinfo = new SHFILEINFO();

            IntPtr hImg = ShellIcon.SHGetFileInfo(
                filePath,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                ShellIcon.SHGFI_ICON | ShellIcon.SHGFI_SMALLICON);

            if (shinfo.hIcon == IntPtr.Zero)
                return null;

            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                shinfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }

        // -------------------------------
        // UI aktualisieren
        // -------------------------------
        private void UpdateFileListUI()
        {
            DateiListe.Items.Clear();

            foreach (var file in gespeicherteDateien)
            {
                if (File.Exists(file))
                {
                    DateiListe.Items.Add(new DateiEintrag()
                    {
                        Icon = GetFileIcon(file),
                        Name = System.IO.Path.GetFileName(file),
                        Path = file
                    });
                }
            }
        }

        // -------------------------------
        // Dateien per Drag & Drop hinzufügen
        // -------------------------------
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (var f in files)
                {
                    if (!gespeicherteDateien.Contains(f))
                        gespeicherteDateien.Add(f);
                }

                SaveFileList();
                UpdateFileListUI();
            }
        }

        // -------------------------------
        // Drag & Drop Sortierung
        // -------------------------------
        private void DateiListe_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStartPoint = e.GetPosition(null);
        }

        private void DateiListe_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                if (DateiListe.SelectedItem is DateiEintrag item)
                {
                    draggedItem = item;
                    DragDrop.DoDragDrop(DateiListe, item, DragDropEffects.Move);
                }
            }
        }

        private void DateiListe_DragLeave(object sender, DragEventArgs e)
        {
            if (draggedItem != null)
            {
                Point pos = e.GetPosition(DateiListe);
                if (pos.X < 0 || pos.Y < 0 || pos.X > DateiListe.ActualWidth || pos.Y > DateiListe.ActualHeight)
                {
                    isDraggingOutside = true;
                }
            }
        }

        private void DateiListe_Drop(object sender, DragEventArgs e)
        {
            // Datei außerhalb losgelassen → entfernen
            if (isDraggingOutside && draggedItem != null)
            {
                gespeicherteDateien.Remove(draggedItem.Path);
                SaveFileList();
                UpdateFileListUI();

                draggedItem = null;
                isDraggingOutside = false;
                return;
            }

            // Sortieren
            if (draggedItem == null)
                return;

            if (e.Data.GetDataPresent(typeof(DateiEintrag)))
            {
                DateiEintrag droppedData = e.Data.GetData(typeof(DateiEintrag)) as DateiEintrag;
                DateiEintrag target = ((FrameworkElement)e.OriginalSource).DataContext as DateiEintrag;

                if (target == null || droppedData == null || droppedData == target)
                    return;

                int newIndex = DateiListe.Items.IndexOf(target);

                gespeicherteDateien.Remove(droppedData.Path);
                gespeicherteDateien.Insert(newIndex, droppedData.Path);

                SaveFileList();
                UpdateFileListUI();

                DateiListe.SelectedIndex = newIndex;
            }

            draggedItem = null;
            isDraggingOutside = false;
        }

        // -------------------------------
        // Datei öffnen (Doppelklick)
        // -------------------------------
        private void DateiListe_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DateiListe.SelectedItem is DateiEintrag eintrag)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = eintrag.Path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Öffnen: " + ex.Message);
                }
            }
        }

        // -------------------------------
        // Kontextmenü: Öffnen
        // -------------------------------
        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            if (DateiListe.SelectedItem is DateiEintrag eintrag)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = eintrag.Path,
                    UseShellExecute = true
                });
            }
        }

        // -------------------------------
        // Kontextmenü: Löschen
        // -------------------------------
        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (DateiListe.SelectedItem is DateiEintrag eintrag)
            {
                gespeicherteDateien.Remove(eintrag.Path);
                SaveFileList();
                UpdateFileListUI();
            }
        }

        // -------------------------------
        // Auto-Close für Expanders
        // -------------------------------
        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            foreach (var child in AccordionPanel.Children)
            {
                if (child is Expander exp && exp != sender)
                    exp.IsExpanded = false;
            }
        }

        // -------------------------------
        // Zweites Fenster öffnen
        // -------------------------------
        private void Button_OpenWindow_Click(object sender, RoutedEventArgs e)
        {
            var fenster = new ZweitesFenster();
            fenster.Show();
        }
    }
}