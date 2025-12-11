namespace UChatServer;

using Microsoft.AspNetCore.SignalR;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq; 

public class DaemonHub : Hub
{
    private readonly IConfiguration _config;

    public DaemonHub(IConfiguration config)
    {
        _config = config;
    }

    // --- CONNECTION SETUP ---
    public override async Task OnConnectedAsync()
    {
        string pid = Environment.ProcessId.ToString();
        await Clients.Caller.SendAsync("ReceiveServerId", pid);
        await base.OnConnectedAsync();
    }

    // --- AUTHENTICATION ---
    public async Task LogIn(string mail, string password)
    {
        string cs = _config.GetConnectionString("DefaultConnection");
        
        long db_id = -1;
        string db_nickname = null;
        string db_hash = null;
        string db_salt = null;

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();

            string readSql = "SELECT id, nickname, paswd, salt FROM user WHERE mail = @mail";
            using (var cmd = new MySqlCommand(readSql, conn))
            {
                cmd.Parameters.AddWithValue("@mail", mail);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        db_id = reader.GetInt64(reader.GetOrdinal("id"));
                        db_nickname = reader.GetString(reader.GetOrdinal("nickname"));
                        db_hash = reader.GetString(reader.GetOrdinal("paswd"));
                        db_salt = reader.GetString(reader.GetOrdinal("salt"));
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveSystem", "System", "User not found");
                        return;
                    }
                }
            }
        }

        var service = new PasswordService();
        bool isMatch = service.VerifyPassword(password, db_hash, db_salt);

        if (isMatch)
        {
            // CRITICAL: This is what was missing in the broken version.
            // We map the ConnectionID to the Nickname so Clients.Group(nick) works.
            await Groups.AddToGroupAsync(Context.ConnectionId, db_nickname);

            await Clients.Caller.SendAsync("LoginSuccess", db_id, db_nickname);
            Console.WriteLine($"[System] User {db_nickname} (ID: {db_id}) logged in.");
        }
        else
        {
            await Clients.Caller.SendAsync("ReceiveSystem", "System", "Wrong password");
        }
    }

    public async Task register(string mail, string password, string user)
    {
        string cs = _config.GetConnectionString("DefaultConnection");
        bool userExists = false;
        bool nicknameTaken = false;

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();

            string checkSql = "SELECT mail, nickname FROM user WHERE mail = @mail OR nickname = @name";
            using (var cmdCheck = new MySqlCommand(checkSql, conn))
            {
                cmdCheck.Parameters.AddWithValue("@mail", mail);
                cmdCheck.Parameters.AddWithValue("@name", user);

                using (var reader = await cmdCheck.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string dbMail = reader.GetString(reader.GetOrdinal("mail"));
                        string dbNick = reader.GetString(reader.GetOrdinal("nickname"));

                        if (string.Equals(dbMail, mail, StringComparison.OrdinalIgnoreCase)) userExists = true;
                        if (string.Equals(dbNick, user, StringComparison.OrdinalIgnoreCase)) nicknameTaken = true;
                    }
                }
            }

            if (!userExists && !nicknameTaken)
            {
                var service = new PasswordService();
                var result = service.HashPassword(password);
                var keyService = new KeyService();
                var keys = keyService.GenerateKeys();

                string insertSql = "INSERT INTO user(mail, paswd, salt, public_key, nickname) VALUES(@mail, @pwd, @sal, @key, @nic)";
                using (var cmdInsert = new MySqlCommand(insertSql, conn))
                {
                    cmdInsert.Parameters.AddWithValue("@mail", mail);
                    cmdInsert.Parameters.AddWithValue("@nic", user);
                    cmdInsert.Parameters.AddWithValue("@pwd", result.HashBase64);
                    cmdInsert.Parameters.AddWithValue("@sal", result.SaltBase64);
                    cmdInsert.Parameters.AddWithValue("@key", keys.PublicKey);
                    await cmdInsert.ExecuteNonQueryAsync();
                }

                await Clients.Caller.SendAsync("ReceiveSystem", "System", "User created successfully.");
                await Clients.Caller.SendAsync("ReceivePrivateKey", keys.PrivateKey);
            }
            else
            {
                if (userExists) await Clients.Caller.SendAsync("ReceiveSystem", "System", "Email already exists.");
                if (nicknameTaken) await Clients.Caller.SendAsync("ReceiveSystem", "System", "Nickname taken.");
            }
        }
    }
    
// Returns a list containing the Chat ID (int) and Chat Type (string)
    public async Task<List<(int Id, string Type)>> GetChats(string username)
    {
        var chats = new List<(int Id, string Type)>();
        string cs = _config.GetConnectionString("DefaultConnection");

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();
            string sql = @"
        SELECT c.id, c.type 
        FROM user u
        JOIN chat_member cm ON u.id = cm.usr_id
        JOIN chats c ON cm.chat_id = c.id
        WHERE u.nickname = @username";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@username", username);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // FIXED LINE BELOW:
                        chats.Add((Convert.ToInt32(reader["id"]), reader["type"].ToString()));
                    }
                }
            }
        }
        return chats;
    }
    
    // --- HELPERS (ADDED FOR GROUPS) ---

    // 1. Get Participants in a Chat (Used for Groups)
    public async Task<List<string>> GetChatParticipants(long chatId)
    {
        var participants = new List<string>();
        string cs = _config.GetConnectionString("DefaultConnection");

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();
            string sql = @"
                SELECT u.nickname 
                FROM chat_member cm
                JOIN user u ON cm.usr_id = u.id
                WHERE cm.chat_id = @cid";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@cid", chatId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        participants.Add(reader.GetString(0));
                    }
                }
            }
        }
        return participants;
    }

    // 2. Batch Public Keys (For Group Encryption)
    public async Task<Dictionary<string, string>> GetPublicKeys(List<string> usernames)
    {
        var keys = new Dictionary<string, string>();
        if (usernames == null || !usernames.Any()) return keys;

        string cs = _config.GetConnectionString("DefaultConnection");
        var parms = usernames.Select((s, i) => $"@u{i}").ToArray();
        string inClause = string.Join(",", parms);

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();
            string sql = $"SELECT nickname, public_key FROM user WHERE nickname IN ({inClause})";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                for (int i = 0; i < usernames.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@u{i}", usernames[i]);
                }

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int nickIdx = reader.GetOrdinal("nickname");
                        int keyIdx = reader.GetOrdinal("public_key");
                        keys[reader.GetString(nickIdx)] = reader.GetString(keyIdx);
                    }
                }
            }
        }
        return keys;
    }

    // --- SEARCH & CHAT CREATION ---

    public async Task SearchUsers(string query)
    {
        var results = new List<string>();
        string cs = _config.GetConnectionString("DefaultConnection");
        
        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();
            string sql = "SELECT nickname FROM user WHERE nickname LIKE @q LIMIT 10";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@q", $"%{query}%");
                using(var reader = await cmd.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        results.Add(reader.GetString(reader.GetOrdinal("nickname")));
                    }
                }
            }
        }
        await Clients.Caller.SendAsync("ReceiveSearchResults", results);
    }

    // Added: CREATE GROUP
    public async Task CreateGroup(string groupName, List<string> participants)
    {
        string cs = _config.GetConnectionString("DefaultConnection");
        long newChatId = 0;

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();
            using (var trans = await conn.BeginTransactionAsync())
            {
                try
                {
                    // 1. Create Chat
                    string createChatSql = "INSERT INTO chats(type) VALUES('group'); SELECT LAST_INSERT_ID();";
                    using (var cmdChat = new MySqlCommand(createChatSql, conn, trans))
                    {
                        newChatId = Convert.ToInt64(await cmdChat.ExecuteScalarAsync());
                    }

                    // 2. Add Members
                    string getUserIdSql = "SELECT id FROM user WHERE nickname = @nick";
                    string addMemberSql = "INSERT INTO chat_member(usr_id, chat_id, status) VALUES(@uid, @cid, @stat)";

                    foreach (var nickname in participants)
                    {
                        long userId = -1;
                        using (var cmdGetId = new MySqlCommand(getUserIdSql, conn, trans))
                        {
                            cmdGetId.Parameters.AddWithValue("@nick", nickname);
                            var result = await cmdGetId.ExecuteScalarAsync();
                            if (result != null) userId = Convert.ToInt64(result);
                        }

                        if (userId != -1)
                        {
                            using (var cmdAdd = new MySqlCommand(addMemberSql, conn, trans))
                            {
                                cmdAdd.Parameters.AddWithValue("@uid", userId);
                                cmdAdd.Parameters.AddWithValue("@cid", newChatId);
                                cmdAdd.Parameters.AddWithValue("@stat", "member");
                                await cmdAdd.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    await trans.CommitAsync();
                }
                catch
                {
                    await trans.RollbackAsync();
                    throw;
                }
            }
        }

        // 3. Notify Participants (Using Groups pattern from LogIn)
        foreach (var user in participants)
        {
            await Clients.Group(user).SendAsync("ReceiveGroupInit", newChatId, groupName, participants);
        }
    }

    public async Task InitPrivateChat(string targetNick, int myId)
    {
        string cs = _config.GetConnectionString("DefaultConnection");
        long targetId = -1;
        long chatId = -1;

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();

            string idSql = "SELECT id FROM user WHERE nickname = @nic";
            using(var cmd = new MySqlCommand(idSql, conn))
            {
                cmd.Parameters.AddWithValue("@nic", targetNick);
                var res = await cmd.ExecuteScalarAsync();
                if(res == null) 
                {
                    await Clients.Caller.SendAsync("ReceiveSystem", "System", "User not found.");
                    return;
                }
                targetId = Convert.ToInt64(res);
            }

            string checkSql = @"
                SELECT c.id 
                FROM chats c
                JOIN chat_member m1 ON c.id = m1.chat_id
                JOIN chat_member m2 ON c.id = m2.chat_id
                WHERE c.type = 'private' 
                  AND m1.usr_id = @myId 
                  AND m2.usr_id = @targetId
                LIMIT 1";

            using (var cmd = new MySqlCommand(checkSql, conn))
            {
                cmd.Parameters.AddWithValue("@myId", myId);
                cmd.Parameters.AddWithValue("@targetId", targetId);
                var existingChatId = await cmd.ExecuteScalarAsync();

                if (existingChatId != null)
                {
                    chatId = Convert.ToInt64(existingChatId);
                    await Clients.Caller.SendAsync("ChatCreated", chatId, targetNick);
                    return;
                }
            }

            using (var trans = await conn.BeginTransactionAsync())
            {
                try 
                {
                    string createChatSql = "INSERT INTO chats(type) VALUES('private'); SELECT LAST_INSERT_ID();";
                    using(var cmd = new MySqlCommand(createChatSql, conn, trans))
                    {
                        chatId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    }

                    string addMembersSql = "INSERT INTO chat_member(usr_id, chat_id, status) VALUES(@uid, @cid, 'member')";
                    
                    using(var cmd = new MySqlCommand(addMembersSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@uid", myId);
                        cmd.Parameters.AddWithValue("@cid", chatId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    
                    using(var cmd = new MySqlCommand(addMembersSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@uid", targetId);
                        cmd.Parameters.AddWithValue("@cid", chatId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    await trans.CommitAsync();
                }
                catch
                {
                    await trans.RollbackAsync();
                    throw;
                }
            }
        }

        await Clients.Caller.SendAsync("ChatCreated", chatId, targetNick);
        await Clients.Group(targetNick).SendAsync("ChatCreated", chatId, "Someone"); 
    }

    // --- MESSAGING ---

    public async Task<string> GetPublicKey(string username)
    {
        string cs = _config.GetConnectionString("DefaultConnection");
        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();
            string sql = "SELECT public_key FROM user WHERE nickname = @nic";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@nic", username);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null) return result.ToString();
            }
        }
        return "NOT_FOUND";
    }

    // UPDATED: Now uses Clients.Group(recipientNick) which works because of LogIn logic
    public async Task SendSecureMessage(long chatId, long senderId, string senderNick, string cipherText, string iv, Dictionary<string, string> keyBundle)
    {
        string cs = _config.GetConnectionString("DefaultConnection");

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();
            using (var trans = await conn.BeginTransactionAsync())
            {
                try
                {
                    // 1. Insert Message
                    long messageId;
                    string sqlMsg = "INSERT INTO messages(chat_id, sender_id, cipher_text, iv) VALUES(@cid, @sid, @cipher, @iv); SELECT LAST_INSERT_ID();";

                    using (var cmd = new MySqlCommand(sqlMsg, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@cid", chatId);
                        cmd.Parameters.AddWithValue("@sid", senderId);
                        cmd.Parameters.AddWithValue("@cipher", cipherText);
                        cmd.Parameters.AddWithValue("@iv", iv);
                        messageId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    }

                    // 2. Insert Keys for each recipient
                    string sqlKey = "INSERT INTO message_keys(message_id, recipient_id, encrypted_session_key) VALUES(@mid, (SELECT id FROM user WHERE nickname=@name), @key)";

                    using (var cmd = new MySqlCommand(sqlKey, conn, trans))
                    {
                        cmd.Parameters.Add("@mid", MySqlDbType.Int64);
                        cmd.Parameters.Add("@name", MySqlDbType.VarChar);
                        cmd.Parameters.Add("@key", MySqlDbType.Text);

                        foreach (var kvp in keyBundle)
                        {
                            cmd.Parameters["@mid"].Value = messageId;
                            cmd.Parameters["@name"].Value = kvp.Key; 
                            cmd.Parameters["@key"].Value = kvp.Value;
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    await trans.CommitAsync();

                    // 3. Notify via SignalR Groups (Relies on LogIn Group assignment)
                    foreach (var recipientNick in keyBundle.Keys)
                    {
                        string userKey = keyBundle[recipientNick];
                        // Pass chatId back so client knows where to display it
                        await Clients.Group(recipientNick).SendAsync("ReceiveSecureMessage", senderNick, cipherText, iv, userKey, chatId);
                    }
                }
                catch (Exception ex)
                {
                    await trans.RollbackAsync();
                    Console.WriteLine("Error sending message: " + ex.Message);
                    throw;
                }
            }
        }
    }

    // --- HISTORY ---

    public class HistoryItem
    {
        public long MessageId { get; set; }
        public string Sender { get; set; }
        public string CipherText { get; set; }
        public string IV { get; set; }
        public string MyEncryptedKey { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public async Task<List<HistoryItem>> GetChatHistory(long chatId, long myId)
    {
        var history = new List<HistoryItem>();
        string cs = _config.GetConnectionString("DefaultConnection");

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();

            string sql = @"
            SELECT 
                m.id AS msg_id,
                u.nickname AS sender_nick, 
                m.cipher_text, 
                m.iv, 
                m.send_time, 
                k.encrypted_session_key
            FROM messages m
            JOIN message_keys k ON m.id = k.message_id
            JOIN user u ON m.sender_id = u.id
            WHERE m.chat_id = @chatId 
              AND k.recipient_id = @myId
            ORDER BY m.send_time ASC";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@chatId", chatId);
                cmd.Parameters.AddWithValue("@myId", myId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        history.Add(new HistoryItem
                        {
                            MessageId = reader.GetInt64(reader.GetOrdinal("msg_id")),
                            Sender = reader.GetString(reader.GetOrdinal("sender_nick")),
                            CipherText = reader.GetString(reader.GetOrdinal("cipher_text")),
                            IV = reader.GetString(reader.GetOrdinal("iv")),
                            MyEncryptedKey = reader.GetString(reader.GetOrdinal("encrypted_session_key")),
                            Timestamp = reader.GetDateTime(reader.GetOrdinal("send_time"))
                        });
                    }
                }
            }
        }
        return history;
    }
}