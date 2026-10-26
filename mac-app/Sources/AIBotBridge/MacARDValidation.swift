import Foundation

enum MacARDValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ value:Bool) throws { guard value else { throw MacTab5LocalError.invalid("ARD fixture failed") }; checks += 1 }
        func hex(_ value:String) -> Data {
            let chars = value.filter { !$0.isWhitespace }; var data = Data(), cursor = chars.startIndex
            while cursor < chars.endIndex { let end = chars.index(cursor,offsetBy:2); data.append(UInt8(chars[cursor..<end],radix:16)!); cursor = end }
            return data
        }
        // Public RFC 3526 group 14; never a saved runtime parameter or key.
        let prime = hex("""
        FFFFFFFF FFFFFFFF C90FDAA2 2168C234 C4C6628B 80DC1CD1
        29024E08 8A67CC74 020BBEA6 3B139B22 514A0879 8E3404DD
        EF9519B3 CD3A431B 302B0A6D F25F1437 4FE1356D 6D51C245
        E485B576 625E7EC6 F44C42E9 A637ED6B 0BFF5CB6 F406B7ED
        EE386BFB 5A899FA5 AE9F2411 7C4B1FE6 49286651 ECE45B3D
        C2007CB8 A163BF05 98DA4836 1C55D39A 69163FA8 FD24CF5F
        83655D23 DCA3AD96 1C62F356 208552BB 9ED52907 7096966D
        670C354E 4ABC9804 F1746C08 CA18217C 32905E46 2E36CE3B
        E39E772C 180E8603 9B2783A2 EC07A28F B5C55DF0 6F4C52C9
        DE2BCBF6 95581718 3995497C EA956AE5 15D22618 98FA0510
        15728E5A 8AACAA68 FFFFFFFF FFFFFFFF
        """)
        var peer = Data(repeating:0,count:256); peer[255] = 2
        let parameters = MacARDConnection.Parameters(generator:2,prime:prime,peer:peer)
        var keys = try MacARDConnection.agree(parameters)
        // Synthetic peer exponent=1 gives shared=our public value, an oracle
        // independent of decryption or another invocation of this wrapper.
        try check(keys.shared == keys.publicValue && keys.shared.count == 256)
        keys.shared.resetBytes(in:0..<keys.shared.count); keys.publicValue.resetBytes(in:0..<keys.publicValue.count)
        let username = Data("fixture-user".utf8), password = Data("fixture-pass!".utf8)
        let output = try MacARDConnection.response(parameters,username:username,password:password,exchange:{ _ in (Data(repeating:0x22,count:256),Data(repeating:0x11,count:256)) },random:{ Data(repeating:0xa5,count:128) })
        let expected = hex("6734e6190fd8c6bb7b390f857f90bafa9b5bc9b8d0a5f69f4e00570c3c60cb209b5bc9b8d0a5f69f4e00570c3c60cb209b5bc9b8d0a5f69f4e00570c3c60cb200a0ada918d50cd628e7fe03b5380f9079b5bc9b8d0a5f69f4e00570c3c60cb209b5bc9b8d0a5f69f4e00570c3c60cb209b5bc9b8d0a5f69f4e00570c3c60cb20")
        try check(Data(output.prefix(128)) == expected && output.count == 384 && Data(output.suffix(256)) == Data(repeating:0x22,count:256))
        for (name,secret) in [(Data(),password),(username,Data()),(Data(repeating:65,count:64),password),(username,Data(repeating:66,count:64)),(username,Data([0])),(username,Data([0xff]))] {
            var exchanged = false
            do { _ = try MacARDConnection.response(parameters,username:name,password:secret,exchange:{ _ in exchanged = true; throw MacTab5Error.unavailable }); throw MacTab5Error.invalidPacket }
            catch is MacTab5LocalError { try check(!exchanged) }
        }
        for invalid in [MacARDConnection.Parameters(generator:1,prime:prime,peer:peer),.init(generator:2,prime:Data(repeating:1,count:128),peer:Data(repeating:2,count:128)),.init(generator:2,prime:prime,peer:Data(repeating:0,count:256)),.init(generator:2,prime:prime,peer:prime)] {
            do { _ = try MacARDConnection.agree(invalid); throw MacTab5Error.invalidPacket }
            catch is MacTab5LocalError { checks += 1 }
        }
        try MacARDConnection.validate(username:Data(repeating:65,count:63),password:Data(repeating:66,count:63)); checks += 1
        return checks
    }
}
