using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Locus.Core.Detection;

namespace Locus.Word;

internal sealed class WordSettingsForm : Form
{
    public WordSettingsForm(WordDetectionSettings settings, Action<WordDetectionSettings> save)
    {
        Text = "Locus — Cặp bọc và nhận diện"; Width = 560; Height = 360;
        StartPosition = FormStartPosition.CenterParent; MinimizeBox = MaximizeBox = false;
        Controls.Add(new Label { Left = 20, Top = 15, Width = 510, Height = 34, Text = "Cặp riêng chỉ định môn cho vùng bên trong. Chuyển vào Word vẫn cần xác nhận." });
        var rows = new List<Tuple<MarkerProfile, CheckBox, TextBox, TextBox>>(); int y = 55;
        foreach (var profile in settings.Profiles.Profiles.OrderBy(p => p.Id == "common" ? 0 : p.Id == "math" ? 1 : p.Id == "physics" ? 2 : 3))
        {
            var enabled = new CheckBox { Left = 20, Top = y + 3, Width = 100, Text = profile.Id == "common" ? "Chung" : profile.Id == "math" ? "Toán" : profile.Id == "physics" ? "Lý" : "Hóa", Checked = profile.Enabled, Enabled = profile.Id != "common" };
            var open = new TextBox { Left = 125, Top = y, Width = 215, Text = profile.Markers.Open, MaxLength = 64, AccessibleName = enabled.Text + " — Dấu mở" };
            var close = new TextBox { Left = 350, Top = y, Width = 150, Text = profile.Markers.Close, MaxLength = 64, AccessibleName = enabled.Text + " — Dấu đóng" };
            Controls.AddRange(new Control[] { enabled, open, close }); rows.Add(Tuple.Create(profile, enabled, open, close)); y += 35;
        }
        Controls.Add(new Label { Left = 20, Top = 205, Width = 170, Text = "Nhận diện ngoài cặp riêng" });
        var math = new CheckBox { Left = 195, Top = 202, Width = 70, Text = "Toán", Checked = (settings.Domains & DetectionDomains.Math) != 0 };
        var physics = new CheckBox { Left = 275, Top = 202, Width = 60, Text = "Lý", Checked = (settings.Domains & DetectionDomains.Physics) != 0 };
        var chemistry = new CheckBox { Left = 350, Top = 202, Width = 70, Text = "Hóa", Checked = (settings.Domains & DetectionDomains.Chemistry) != 0 };
        var button = new Button { Left = 400, Top = 260, Width = 100, Text = "Lưu" };
        Controls.AddRange(new Control[] { math, physics, chemistry, button });
        button.Click += (_, _) => {
            try {
                save(new WordDetectionSettings(new MarkerProfileSet(rows.Select(r => new MarkerProfile(r.Item1.Id, r.Item1.Domain, r.Item3.Text, r.Item4.Text, r.Item2.Checked))),
                    (math.Checked ? DetectionDomains.Math : 0) | (physics.Checked ? DetectionDomains.Physics : 0) | (chemistry.Checked ? DetectionDomains.Chemistry : 0)));
                Close();
            } catch (Exception error) { MessageBox.Show(this, error.Message, "Locus"); }
        };
    }
}
