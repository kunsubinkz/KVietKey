import XCTest
@testable import VietEngine

/// Lỗi phát hiện khi gõ thử văn bản dài (stress test).
final class RegressionTests: EngineTestCase {
    func testRepeatedToneKeyFarApartIsLiteral() {
        check([
            ("response ", "response "), ("order ", "order "), ("p99 response time ", "p99 response time "),
            ("user_profile_v2 ", "user_profile_v2 "), ("order-service#409 ", "order-service#409 "),
        ])
    }

    func testAdjacentRepeatStillUndoes() {
        check([("ass ", "as "), ("tieengss ", "tiêngs "), ("ddd ", "dd "), ("aaa ", "aa ")])
    }

    /// Gõ nhanh: dấu mũ / d / w gõ sau (lấy từ vết gõ thật của người dùng).
    func testLateModifiersFromRealTyping() {
        check([
            ("nhapaj ", "nhập "), ("chamaj ", "chậm "), ("loiox ", "lỗi "), ("cuoios ", "cuối "),
            ("dudowcj ", "được "), ("nuawx ", "nữa "), ("nuasw ", "nứa "), ("muaw ", "mưa "),
            ("cuarw ", "cửa "), ("tienge ", "tiêng "), ("vietej ", "việt "), ("dud ", "đu "),
            ("quaw ", "quă "), ("banana ", "banana "), ("home ", "home "), ("muaww ", "muaw "),
            ("ngoaawfn ngoeof ", "ngoằn ngoèo "), ("khoeo ", "khoeo "),
        ])
    }

    func testLoanSyllablesWithFinalK() {
        check([("DDAWKS LAWKS ", "ĐẮK LẮK "), ("ddawks lawks ", "đắk lắk "), ("Ddawks ", "Đắk ")])
    }
}
