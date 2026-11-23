import XCTest
@testable import VietEngine

/// Sinh chuỗi phím từ từ tiếng Việt chuẩn rồi kiểm tra engine tái tạo đúng từ đó.
final class CorpusTests: XCTestCase {
    static let words = """
    tiếng việt người được trường thương đường những nhưng không chúng chào xin cảm ơn bạn tôi anh em
    hoà bình thuý khoẻ hoàn toàn quốc gia giáo dục nghiêng nghĩa nghề nghiệp khuya khuyên khuyến
    quyền quý quả quán quyết huỳnh huyền thuở mưa chưa cửa nước rượu hươu yêu thiêu nhiều điều
    đẹp đây đi đến đã đó đều đang đúng đủ đồng đại đầu đường tuần tuổi buổi cuối muốn xuống
    trời trên trước trong trăm trắng trường trưa trứng trẻ trẻ chuyện chuyển chuẩn chuông chương
    học hỏi hiểu hiện hình hành hạnh hãy hết hơn hợp họ hàng hạt hơi hôm hồ hồng hổ
    nhà nhớ nhẹ nhanh nhân nhật nhiên nhóm nhũng như nhưng nữa nửa nếu nên nói nơi nào nấu
    lớn lúc lên lòng làm lại lạnh lắm lễ lịch lửa lưu lương lượng lược lần lẽ
    mình một mọi mỗi mới mùa mưa máy mặt mắt mẹ mềm mệt mất mấy mười
    sách sáng sau sạch sinh sống sức sửa sự số sớm sẽ sữa suốt
    thế thì thời thứ thật thấy thầy thành thích thêm thiết thường thuận thủ thức thực thuế
    việc vui vào vì vậy về vẫn vừa vững vườn vết viết vị
    gì giờ giữ giúp giải giản giá giàu gió gái gần gặp ghi ghế gỗ gọi
    kết khi khó khác khách khổ khoẻ không khuôn kỳ kĩ kế kiến kiểm kích
    cần cách cái cả các còn có cũng cuộc của cứ cười cao cấp cầu chỉ chị chủ chính chúc
    bao bài bàn bán bản bằng bên bệnh biết bình bỏ bốn bước bữa bóng bát
    ra rất rồi rõ rộng rơi rừng rửa rẻ
    xanh xe xem xây xã xứ xong xóm xuân xuất
    yên ý ánh áo ăn ăng ấm ân ép êm ít ích ông ơi ở ủng ừ
    tay tên tìm tình tốt tới tờ tục từ tử tự tại tập tấm tắt tặng tháng thân
    đất đáng đau điểm đời đôi đón đỏ độ động đức
    phải phố phòng phép phát phim phút phương phụ phục phía phở
    qua quê quen quân quanh quạ quý quỷ
    nghe nghỉ nghìn nghị ngày ngay ngoài ngoại ngôi ngôn ngủ ngữ ngựa ngọt ngực
    oanh ngoặc loạn khoa toán hoạ hoặc xoáy
    """.split(whereSeparator: { $0 == " " || $0.isNewline }).map(String.init)

    private func unique(_ list: [String]) -> [String] { Array(Set(list)).sorted() }

    /// Telex: dấu mũ gõ đôi, `w` cho trăng/móc, `dd` cho đ, dấu thanh gõ cuối từ.
    static func telexKeys(for word: String) -> String {
        var keys = "", tone = ""
        for scalarString in word.map({ String($0) }) {
            var s = scalarString.decomposedStringWithCanonicalMapping
            let lower = s.lowercased()
            if lower == "đ" { keys += scalarString.first!.isUppercase ? "DD" : "dd"; continue }
            var base = ""
            for u in s.unicodeScalars {
                switch u {
                case "\u{0302}": base += String(base.last!)
                case "\u{0306}", "\u{031B}": base += "w"
                case "\u{0301}": tone = "s"
                case "\u{0300}": tone = "f"
                case "\u{0309}": tone = "r"
                case "\u{0303}": tone = "x"
                case "\u{0323}": tone = "j"
                default: base += String(u)
                }
            }
            s = base
            keys += s
        }
        return keys + tone
    }

    static func vniKeys(for word: String) -> String {
        var keys = "", tone = ""
        for ch in word {
            let s = String(ch)
            if s.lowercased() == "đ" { keys += (ch.isUppercase ? "D" : "d") + "9"; continue }
            var base = "", marks = ""
            for u in s.decomposedStringWithCanonicalMapping.unicodeScalars {
                switch u {
                case "\u{0302}": marks += "6"
                case "\u{0306}": marks += "8"
                case "\u{031B}": marks += "7"
                case "\u{0301}": tone = "1"
                case "\u{0300}": tone = "2"
                case "\u{0309}": tone = "3"
                case "\u{0303}": tone = "4"
                case "\u{0323}": tone = "5"
                default: base += String(u)
                }
            }
            keys += base + marks
        }
        return keys + tone
    }

    private func capitalized(_ w: String) -> String { w.prefix(1).uppercased() + w.dropFirst() }

    func testTelexCorpus() {
        var count = 0
        for word in unique(Self.words) + unique(Self.words).map(capitalized) {
            let keys = Self.telexKeys(for: word)
            XCTAssertEqual(Typist.run(keys + " "), word + " ", "telex keys: \(keys)")
            count += 1
        }
        XCTAssertGreaterThan(count, 500)
    }

    func testVNICorpus() {
        for word in unique(Self.words) {
            let keys = Self.vniKeys(for: word)
            XCTAssertEqual(Typist.run(keys + " ") { $0.method = .vni }, word + " ", "vni keys: \(keys)")
        }
    }

    func testCombinedAcceptsBothStyles() {
        for word in unique(Self.words).prefix(200) {
            XCTAssertEqual(Typist.run(Self.telexKeys(for: word) + " ") { $0.method = .combined }, word + " ")
            XCTAssertEqual(Typist.run(Self.vniKeys(for: word) + " ") { $0.method = .combined }, word + " ")
        }
    }

    func testEveryCorpusWordIsValidSyllable() {
        for word in unique(Self.words) {
            let t = Typist()
            t.type(Self.telexKeys(for: word))
            // Nếu từ hợp lệ, gõ dấu cách không được làm thay đổi từ.
            let before = t.screen
            t.type(" ")
            XCTAssertEqual(t.screen, before + " ", word)
        }
    }

    func testLegacyCharsetsEndToEnd() {
        for cs in Charset.allCases {
            for word in unique(Self.words).prefix(80) {
                XCTAssertEqual(Typist.run(Self.telexKeys(for: word)) { $0.charset = cs }, cs.encode(word), "\(cs) \(word)")
            }
        }
    }
}
