# Dùng và phát triển core M1

Core này hiện được dùng trong [Desktop M2](../m2/REPORT.md). `tools/build.ps1` đã được mở rộng để chạy cả tests Desktop trên Windows; giới hạn core bên dưới giữ nguyên.

## Chạy từ repository

Cần SDK .NET **10.0.400** theo `global.json`. Trên baseline Windows hiện tại đã có .NET Framework 4.8 reference assemblies/runtime để build và chạy probe x86. Không cần Office cho core, CLI hoặc preview.

```powershell
./tools/build.ps1
dotnet run --project src/Locus.Cli -c Release --no-build -- --input 'x mũ 2'
dotnet run --project src/Locus.Cli -c Release --no-build -- --input 'Ta có lc[q^7].' --mode marked
dotnet run --project src/Locus.Cli -c Release --no-build -- --serve 4180
```

Mở `http://127.0.0.1:4180/` để nhập tự do, chọn cách nhận diện và cặp dấu, xem MathML và lấy văn bản xuất. Ctrl+C tại terminal chạy server để dừng. Đây là web host thử trên loopback, không phải dịch vụ tự khởi động Windows. Input chỉ ở bộ nhớ; build/restore lần đầu có thể cần tải reference packages, còn phân tích/render không gọi mạng ngoài.

`build.ps1` dùng locked restore, build Release và chạy console test harness; exit khác 0 khi lỗi. Test harness không dùng `dotnet test`. Trên Windows, script còn chạy so hai runtime với DLL thật. `-SkipTests` chỉ phục vụ build nhanh; `-SkipRuntimeProbe` không có nghĩa probe đã đạt ở máy khác.

Core chỉ dùng BCL và reference package `NETStandard.Library` 2.0.3 cùng dependency reference được khóa trong `packages.lock.json`; không thêm thư viện parser/renderer ngoài. CLI dùng shared framework ASP.NET Core của .NET 10. Các project đều có lockfile. Thay dependency phải cập nhật lockfile có chủ ý rồi chạy lại kiểm chứng; không tắt locked restore để che dependency drift.

## API tối thiểu

```csharp
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Serialization;

var source = new SourceSnapshot("Ta có lc[x mũ 2].", revision: 1);
var result = new AnalysisEngine().Analyze(source,
    new AnalysisOptions(InputMode.Markers));

var region = result.Regions[0];
var candidate = region.Candidates[0];
var output = CandidateExporter.Export(candidate);
string mathMl = output.MathMl;
string omml = output.Omml;
string latex = output.Latex;
string rawWithMarkers = output.OriginalText;

string json = CandidateSetSerializer.Serialize(region.Select(candidate.Id));
CandidateSet restored = CandidateSetSerializer.Deserialize(json);
```

Trong ứng dụng thật phải kiểm tra có regions/candidates trước khi truy cập, trình bày diagnostics và yêu cầu lựa chọn khi cần. Ví dụ trên chỉ minh họa nguồn hợp lệ đã biết.

`ContentEligibility == "eligible"` nghĩa **nội dung đơn nghĩa và không có repair/warning/error**. Nó không cấp quyền ghi. Core không đọc focus/IME/Word revision và không có API commit Word. Chọn một repair không biến repair thành kết quả đủ điều kiện auto.

`OriginalText` của exporter trả phần replacement nguyên văn, gồm cặp bọc nếu có; `OriginalContent` chỉ phần bên trong. Toàn bộ văn xuôi ngoài vùng nằm trong `Source.Raw`. `SourceEdit.Apply` áp dụng các edit theo tọa độ nguồn gốc; không dùng nó trực tiếp làm adapter Word.

## Dữ liệu và định danh

- Source/raw, AST, candidate và collections bất biến. ID xác định từ dữ liệu và phiên bản; cùng snapshot có cùng ID trên các runtime đã thử.
- Mọi span dùng UTF-16 code unit half-open. Constructor kiểm phạm vi và surrogate boundary; mỗi node có source references. CandidateSet kiểm source identity, thứ tự/count, vùng và cặp dấu.
- `NormalizedSource.Create` tạo NFC/toán tử/whitespace phục vụ xử lý và map nhiều–nhiều. `ProjectToSource` trả tập span nguồn; một vị trí trong phần được compose/expand có thể không có caret nguồn duy nhất.
- Parser đọc token theo raw span, NFC từng token; không lấy offset từ chuỗi normalized để thay nguồn. Alias dùng lexicon chung cho parser và detector.
- CandidateSet lưu toàn bộ kết quả kể cả chưa mở UI lựa chọn. Serializer đọc cây đã lưu và đối chiếu identity; không gọi parser.

## Giới hạn M1

| Thành phần | Mức áp dụng |
| --- | --- |
| Analysis source | Mặc định 65.536 UTF-16; tùy chọn đến 1.048.576 |
| Một vùng parser | Tối đa 4.096 UTF-16; caller có thể chọn thấp hơn |
| Parser/AST do parser tạo | Độ sâu tối đa 64, gồm cả cây trái dài |
| Số vùng | Mặc định 64; tùy chọn tối đa 1.024 |
| Candidate | Tối đa 3; nhiều điểm mơ hồ bị từ chối thay vì cắt thành đơn nghĩa |
| Marker | Mỗi dấu 1–64 UTF-16; literal, khác nhau/non-prefix, không CR/LF/backslash/unpaired surrogate |
| Snapshot | JSON tối đa 8 Mi ký tự; raw tối đa 1 Mi UTF-16; tổng tối đa 10.000 node, sâu 128 và 50.000 source references |
| HTTP preview | Source tối đa 65.536 UTF-16; body 262.144 byte; JSON-only, đúng host/origin local |

Giới hạn resource là quyết định implementation M1, không phải cam kết Word có thể chứa payload Tag lớn tương ứng. Không cắt nguồn hoặc candidate cho vừa giới hạn; trả chẩn đoán hoặc từ chối dữ liệu.

`LatestRequestGate<T>` cần một instance cho mỗi editor/session. Gọi `Invalidate()` khi cấu hình/session/focus nhập của editor thay đổi; chỉ công bố `IsCurrent` ở thời điểm hoàn tất. Khi ghi ứng dụng đích sau đó, vẫn phải kiểm tra lại snapshot và trạng thái đích. Gate không thay thế transaction hoặc quyền ghi.

## Mở rộng đúng chỗ

Sửa ngữ pháp trong `Parsing`, định vị vùng trong `Detection`, định dạng xuất trong `Export`. Không thêm parser JavaScript vào preview. Thêm case hồi quy cùng sửa lỗi, ghi errata khi đặc tả cũ thiếu hoặc mâu thuẫn; không sửa expected chỉ để che lỗi implementation.

Các bước M2: chọn host Desktop/renderer SVG, editor nhập tiếng Việt và trạng thái composition thật, selection/history, copy/export SVG/PNG/text và thử dán vào ứng dụng đích. Dùng lại core này. Các việc Word W0 còn mở không chặn Desktop độc lập.
