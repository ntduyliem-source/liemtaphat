# Locus

Locus hỗ trợ nhập công thức bằng cách gõ tự nhiên, ưu tiên cách diễn đạt tiếng Việt. Desktop phục vụ nhập, xem trước và sao chép độc lập; connector Word đưa kết quả vào tài liệu dưới dạng công thức native, có thể chỉnh sửa và khôi phục.

## Trạng thái hiện tại

**G alpha local đạt 14/14 task**, chốt ngày 30/09/2026. Web/Desktop có Công thức Toán/Lý/Hóa, Đồ thị và Hình học 2D/3D; dùng chung mã C#. Ô kết quả Web/Desktop đã bỏ fx; chọn công thức để mở **Chi tiết công thức** bên dưới. Đồ thị có **Cách hiểu**, nhiều đường/tham số/slider; hình học kéo điểm/cạnh, snap, góc/phép dựng, camera và mặt phẳng 3D. Có `.locus`, nháp, PNG/SVG và xuất nguyên đoạn DOCX/HTML. [Phạm vi G](docs/phase-g/REPORT.md), [biên bản kiểm máy](docs/host-review/20260930.md), [backlog hiện tại](docs/BACKLOG.md).

Repository chứa mã nguồn, test, corpus/fixture, prototype và tài liệu. `artifacts/`, output build, cache, `node_modules` và profile WebView không được đưa vào Git. Các liên kết `artifacts/` trong báo cáo lịch sử là bằng chứng trên máy phát triển, không có sẵn sau clone. H/WEB2 chưa triển khai; các cổng IME, tải tệp Web và Word nhiều DPI/màn hình còn mở theo biên bản.

## Build và test từ source

Máy build: Windows x64, PowerShell 7, .NET SDK **10.0.400** theo `global.json`, Node.js **22+** và Edge WebView2 Runtime cho Desktop. Lần restore đầu cần tải các gói NuGet/npm theo lockfile.

```powershell
npm --prefix tools/sh ci --ignore-scripts
node tools/web1/vendor.mjs
New-Item -ItemType Directory -Force artifacts/phase-g | Out-Null
./tools/phase-g/build.ps1
./tools/phase-g/local.ps1 -Port 4193
```

Mở `http://127.0.0.1:4193/`. Nếu cổng đã dùng, chọn cổng trống bằng `-Port`. Dừng server bằng `./tools/phase-g/local.ps1 -Action stop -Port 4193`. Sau build, mở Desktop bằng `./tools/phase-g/desktop.ps1`.

Các bộ kiểm là chương trình console:

```powershell
dotnet run --project tests/Locus.Core.Tests -c Release -- --root (Get-Location).Path
dotnet run --project tests/Locus.Application.Tests -c Release -- --content-only artifacts/qa/content
dotnet run --project tests/Locus.Application.Tests -c Release -- --balance-only artifacts/qa/balance
dotnet run --project tests/Locus.Application.Tests -c Release -- --plot-only artifacts/qa/plot
dotnet run --project tests/Locus.Application.Tests -c Release -- --geometry-only artifacts/qa/geometry
```

ZIP source giải nén ngày 30/09 đã build lại Web/Desktop portable thành công và đạt core 240/240, content 22/22, balance 20/20, plot 29/29, geometry 16/16. Đây là kiểm logic/build; nghiệm thu UI/Word/IME có phạm vi riêng trong biên bản.

Word connector nằm ở `src/Locus.Word`, dùng Word x86/.NET Framework 4.8 và Office interop assemblies; build bằng `./tools/phase-f/build.ps1`. [Hướng dẫn test cho Dot](docs/handoff/DOT-LOCAL-TEST.md), [brief cho designer](docs/handoff/DESIGNER-WEB-BRIEF.md), [hướng dẫn Word F](docs/phase-f/QUICKSTART.md).

## Các baseline và lịch sử thực hiện

**Đợt D đã có bản dùng thử trên [Web local](http://127.0.0.1:4189/): xuất nguyên đoạn/vùng chọn ra DOCX OMML hoặc HTML MathML, Desktop có tray/bung/thu và lưu tệp native.** Vòng DOCX mở/lưu/đóng/mở lại Word đã đạt; D còn file hai host, clipboard/download thực tế và thao tác tray/IME. [Báo cáo D](docs/doc1/REPORT.md), [cách dùng và bài còn thiếu](docs/doc1/QUICKSTART.md). Cân bằng/hủy, batch/hoàn tác, auto mặc định tắt và file v7 giữ nguyên baseline [C](docs/bal1/REPORT.md).

**E3 Hóa/Lý đạt alpha local, 8/8 task.** Cùng giao diện Toán, thêm ba checkbox; nguồn/candidate, SVG/PNG, tệp/nháp dùng chung Web/Desktop. Word x86 thủ công đạt 33/33 kiểm tra native trên baseline E3. [Mở E3 local](http://127.0.0.1:4184/), [cách dùng và cài Word](docs/e3/QUICKSTART.md), [báo cáo](docs/e3/REPORT.md). **SC1-01…07 đã đạt local**: cặp bọc, cân bằng, kho 45 bản ghi có điều kiện và ghost/Enter/Undo; **SC1-09 đã đạt Word thủ công ở E**. SC1-08/10 giữ phần kiểm còn thiếu. [Baseline trước E](docs/BASELINE-20260915.md), [ghost đã nghiệm thu](docs/sc1/GHOST-REPORT.md). Các mục dưới ghi các baseline đã đạt trước E3.

M2 đã có Desktop alpha Windows chạy độc lập, dùng core C# chung và xuất SVG/PNG/văn bản. **M3 đã có Word alpha chuyển vùng chọn thành equation native sau xác nhận**, Undo/Redo, khôi phục nguồn và tách quản lý.

- **M2 alpha đã triển khai.** [Báo cáo M2](docs/m2/REPORT.md), [hướng dẫn sử dụng](docs/m2/QUICKSTART.md).
- **M3 alpha đạt 4/4 task trên Word x86 đã kiểm; G1 mở cho chuyển thủ công.** 38 nhóm Word, 7 nhóm preview và 10 kiểm tra bàn phím đạt. Gói cài/cài lại/gỡ và 7 nguồn trong gói đã kiểm. [Báo cáo M3 và giới hạn](docs/m3/REPORT.md), [dùng Word](docs/m3/QUICKSTART.md).
- **W0 đã nghiệm thu 4/6 hạng mục (67%), CW mở cho chuyển thủ công.** Đã thêm badge bám vùng và hai biến thể nhập nối A/B trong Word, thử native/nguồn/Undo bằng API và Windows input. Còn quan sát trực tiếp fx/DPI và phản hồi người thử để chốt D-01. [Checklist để làm khi rảnh](docs/w0/PENDING-ACCEPTANCE.md), [báo cáo và số kiểm tra](docs/w0/REPORT.md).
- Build Release đạt 238/238 kiểm tra core và 40/40 nhóm Desktop; Telex/VNI thật và 8 bài clipboard Word đã được kiểm. Gói portable chạy 6 nguồn bằng runtime đi kèm. Cùng DLL core cho cùng kết quả trên .NET 10 và .NET Framework 4.8 x86 với 13 nguồn thử.
- Core chọn C# dùng chung; Word theo hướng COM trên Windows. CW/G1 đạt cho thủ công trên baseline; G2/G3 chưa đạt, chưa phát hành tự chuyển marker/Space.
- Cặp bọc mặc định **`lc[...]`**, đổi được cả dấu mở và dấu đóng. Space vẫn là quyết định riêng còn mở.
- **WEB0 đạt 3/3 task thử kiến trúc.** Core C# chạy bằng WebAssembly từ tệp tĩnh; 116 nguồn tương đương native, 238 contracts trên mỗi host; cùng editor nhỏ trong browser và WPF Hybrid. Có canvas kéo điểm/Undo/SVG, thử JSXGraph và bằng chứng Telex trên WASM WebView2. [Kết quả và giới hạn](docs/web/COMPATIBILITY.md), [cách chạy](docs/web/QUICKSTART.md). WEB0 là baseline prototype; WEB1 alpha local đã có bên dưới, chưa xuất bản public.

**SH đã đạt 4/4 task nền editor chung.** Web/Desktop dùng chung session, tệp `.locus`, renderer SVG/PNG và giao diện; worker C# xử lý ngoài UI browser. Đạt 32 nhóm Application, 104 case worker/host và 14 nhóm UI/host. [Báo cáo SH](docs/sh/REPORT.md), [dùng thử](docs/sh/QUICKSTART.md).

**WEB1 đạt alpha local, 4/4 task nghiệm thu (100% mốc WEB1):** clipboard/tải dự phòng, nháp tự phục hồi, phím tắt/preferences, offline/cập nhật và gói mở local. Telex/VNI thật đã kiểm trên đúng WASM trong Windows WebView2; [phạm vi IME](docs/web1/IME.md). Người dùng đã hoãn đưa lên Sites. [Mở WEB1](http://127.0.0.1:4183/), [bộ mở lại](tools/web1/Start-Locus-Web.cmd), [báo cáo](docs/web1/REPORT.md), [hướng dẫn](docs/web1/QUICKSTART.md).

**Đợt E đã có Word thủ công SC1-09 và gói review local:** bốn cặp, cân bằng/hủy, nhận sản phẩm, cập nhật native/Undo và metadata v2 đọc được v1. [Báo cáo E](docs/phase-e/REPORT.md), [dùng thử](docs/phase-e/QUICKSTART.md), [Web E local](http://127.0.0.1:4191/). SC1 đạt **8/10 task**; SC1-10 còn nghiệm thu host. D đã khép DOCX/Word, còn tray/file/clipboard/IME thật theo [báo cáo D](docs/doc1/REPORT.md).

**Đợt F có Word 0.5.0 review local:** quét vùng chọn/thân tài liệu, bảng fx, chuyển riêng/nhóm trong một Undo và giữ quyết định về text qua DOCX. WD1-01/03/04 đạt; WD1-02/05 còn nghiệm thu fx inline/host. [Báo cáo F](docs/phase-f/REPORT.md), [cách dùng](docs/phase-f/QUICKSTART.md). Web/Desktop tiếp tục ở bản E.

**Thứ tự tiếp theo:** H/WEB2 chốt cách tính lượt, tài khoản và quota; song song giữ checklist review Desktop của G và các cổng host D/SC1/WD1 để làm khi người dùng/công cụ sẵn sàng. Ghost trực tiếp trong Word và auto-Space theo cổng riêng. [Các bước cụ thể](docs/NEXT-STEPS.md), [roadmap](docs/ROADMAP.md), [backlog](docs/BACKLOG.md).

## Chạy Desktop và công cụ thử

Bản G hiện tại: [Web local 4193](http://127.0.0.1:4193/), [gói và SHA-256](artifacts/phase-g/packages.json), [hướng dẫn](docs/phase-g/QUICKSTART.md). Gói Web cần Node.js 22+; gói Desktop x64 portable đã kèm .NET và cần WebView2 Runtime. Đây chưa phải bộ cài MSI/Setup.

Bản thử SC1 Web/Desktop: [Web local 4185](http://127.0.0.1:4185/), [cách mở lại](docs/sc1/QUICKSTART.md). Chưa có gói SC1 toàn mốc đã nghiệm thu. Bản có gói baseline WEB1: [Web WEB1](http://127.0.0.1:4183/), [ZIP Web local](artifacts/releases/Locus-WEB1-20260914-070957-601-alpha-local-Web-static.zip), [ZIP Desktop](artifacts/releases/Locus-WEB1-20260914-070957-601-alpha-local-Desktop-win-x64.zip). Mở lại Web bằng `tools/web1/Start-Locus-Web.cmd`. [Hướng dẫn](docs/web1/QUICKSTART.md).

Baseline SH giữ để đối chiếu: [gói Desktop SH](artifacts/releases/Locus-SH-Desktop-win-x64.zip), [gói static SH](artifacts/releases/Locus-SH-Web-static.zip). Desktop SH cần .NET 10 Desktop Runtime/WebView2; WEB1 bổ sung nháp và offline reload.

Bản Web thử: [Locus WEB0](http://127.0.0.1:4181/) khi server tĩnh đang chạy. Build bằng `./tools/web0/build.ps1`, serve bằng `node tools/web0/serve.mjs artifacts/web/browser/wwwroot 4181`. [Gói static WEB0](artifacts/releases/Locus-Web-WEB0-static.zip), [hướng dẫn và giới hạn](docs/web/QUICKSTART.md). Parser chạy trong browser; URL localhost hiện tại chỉ truy cập trên máy này.

Baseline Desktop M2: mở [Locus.Desktop.exe](artifacts/releases/Locus-0.2.0-alpha-win-x64/Locus.Desktop.exe), hoặc giải nén [gói Windows x64](artifacts/releases/Locus-0.2.0-alpha-win-x64.zip). Không cần SDK, Word hoặc server preview để dùng gói này. [Giới hạn clipboard/bộ gõ và baseline đã thử](docs/m2/REPORT.md).

Baseline Word M3: [Locus Word 0.3.0 alpha x86](artifacts/releases/Locus-Word-0.3.0-alpha-x86.zip). Đóng Word, giải nén và chạy `./register.ps1 -Action Install` trong thư mục gói; mở Word → tab **Locus**. [Hướng dẫn cài/gỡ và bàn phím](docs/m3/QUICKSTART.md). Desktop M2 vẫn có tab kết nối nghiên cứu W0; để dùng M3 hãy dùng Ribbon Word.

Chạy Desktop từ source: `dotnet run --project src/Locus.Desktop -c Release --no-build` sau build. Phần dưới giữ cách chạy công cụ thử M1.

```powershell
./tools/build.ps1
dotnet run --project src/Locus.Cli -c Release --no-build -- --serve 4180
```

Mở [bản xem thử M1](http://127.0.0.1:4180/): nhập tự do, đổi cặp dấu, chọn candidate và xem MathML. Parser chạy bằng core C# thật; xuất LaTeX, OMML và nguồn nguyên văn. SVG/PNG có trong Desktop M2. Cần .NET SDK theo `global.json` để chạy công cụ thử; [chi tiết](src/Locus.Cli/README.md).

## Lưu trữ bản thử M0

[Demo cặp bọc](prototypes/m0-space/markers.html) và [so sánh Space](prototypes/m0-space/index.html) dùng fixture cục bộ, chưa phải parser sản phẩm. Chạy `node prototypes/m0-space/serve.cjs`, mở địa chỉ localhost in trong terminal rồi vào `/markers.html`. [Hướng dẫn và bài thử](docs/m0/MARKER-EXPERIMENT.md).

## Tài liệu để bám thực hiện

| Tài liệu | Mục đích |
| --- | --- |
| [Hợp đồng sản phẩm](docs/PRODUCT.md) | Hành vi phải giữ, phạm vi và những điểm chưa quyết định |
| [Roadmap](docs/ROADMAP.md) | Thứ tự triển khai, phụ thuộc và điều kiện hoàn thành từng mốc |
| [Các bước thực hiện tiếp theo](docs/NEXT-STEPS.md) | Hàng đợi từng task, việc cần làm, demo và điều kiện hoàn thành từng đợt |
| [Thiết kế Web và editor](docs/WEB-AND-VISUAL-EDITORS.md) | Web dùng chung logic, đồ thị/hình học, repo tham khảo và cách kiểm chứng công nghệ |
| [Backlog](docs/BACKLOG.md) | Công việc cụ thể, trạng thái và bằng chứng nghiệm thu |
| [Kiểm chứng kỹ thuật](docs/TECHNICAL-DISCOVERY.md) | Những thử nghiệm quyết định kiến trúc Word và nguồn tham khảo |
| [Báo cáo M1](docs/m1/REPORT.md) | Core đã triển khai, kết quả kiểm tra và giới hạn |
| [Dùng core M1](docs/m1/CORE-USAGE.md) | API, build, dependency và cách mở rộng |
| [Báo cáo M2](docs/m2/REPORT.md) | Desktop, xuất ảnh, bằng chứng và giới hạn alpha |
| [Dùng Desktop](docs/m2/QUICKSTART.md) | Cài/chạy portable, cặp dấu và cách copy/xuất |
| [Báo cáo M3](docs/m3/REPORT.md) | Chuyển thủ công Word, G1 theo baseline, kết quả và giới hạn alpha |
| [Dùng Word](docs/m3/QUICKSTART.md) | Cài/gỡ, chọn vùng, xác nhận, Undo, restore và detach |
| [Báo cáo W0](docs/w0/REPORT.md) | Add-in nghiên cứu, native/metadata/Undo, lifecycle và các bằng chứng còn thiếu |
| [Checklist nghiệm thu W0](docs/w0/PENDING-ACCEPTANCE.md) | Việc cần người dùng thử, phần Codex đã kiểm và cách mở lại bản thử |
| [Báo cáo M0](docs/m0/REPORT.md) | Kết quả thực tế, FAIL/UNTESTED và trạng thái từng cổng |
| [Quyết định M0](docs/m0/DECISIONS.md) | Grammar, nguồn/candidate, runtime, connector và metadata |
| [Kế hoạch lập sau M0](docs/m0/NEXT-STEPS.md) | Lưu trữ đầu vào M1 và các việc Word còn thiếu; trạng thái hiện tại theo backlog |

## Cách dùng kế hoạch

1. Bắt đầu từ mốc hiện tại trong roadmap và chọn task đủ điều kiện ở backlog.
2. Chốt tiêu chí nghiệm thu của task trước khi viết mã.
3. Khi hoàn thành, ghi đường dẫn đến mã, kết quả thử hoặc bản demo trong cột bằng chứng.
4. Chỉ đóng mốc khi các điều kiện ra khỏi mốc đã đạt; có tính năng chạy được chưa đủ để đóng mốc.
5. Nếu quyết định sản phẩm thay đổi, cập nhật hợp đồng và các task liên quan trong cùng đợt làm việc.

Tài liệu được cập nhật cùng các lần thực hiện dự án. Hiện chưa thiết lập lịch tự động theo dõi hoặc chạy công việc.
