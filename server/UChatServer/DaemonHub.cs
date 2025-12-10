namespace UChatServer;
using Microsoft.AspNetCore.SignalR;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;

public class DaemonHub : Hub
{
    //load config for mysql
    private readonly IConfiguration _config;

    public DaemonHub(IConfiguration config)
    {
        _config = config;
    }

    //connection confirmation
    public override async Task OnConnectedAsync()
    {
        // Send the PID to the client so they know which process they hit
        string pid = Environment.ProcessId.ToString();
        await Clients.Caller.SendAsync("ReceiveServerId", pid);

        await base.OnConnectedAsync();
    }


    //TODO: add checks for chats and functionality
    public async Task SendMessage(string user, string message)
    {
        Console.WriteLine($"[Daemon Log] {user} says: {message}");
        await Clients.All.SendAsync("ReceiveSystem", user, message);
    }

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

    public async Task LogIn(string mail, string password)
    {
        string cs = _config.GetConnectionString("DefaultConnection");
        string db_hash, db_salt;
        using (var conn = new MySqlConnection(cs))
        {
            conn.Open();

            string readSql = "SELECT paswd, salt FROM user WHERE mail = @mail";
            using (var cmd = new MySqlCommand(readSql, conn))
            {
                cmd.Parameters.AddWithValue("@mail", mail);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read() && null != reader.GetString(0))
                    {
                        db_hash = reader.GetString(0);
                        db_salt = reader.GetString(1);
                    }
                    else
                    {
                        await Clients.All.SendAsync("ReceiveSystem", "eeror", "no user found");
                        return;
                    }
                }
            }
        }

        //check password
        var service = new PasswordService();
        bool isMatch = service.VerifyPassword(password, db_hash, db_salt);
        //TODO:terminatre connection after 3 attemtss (not here)
        await Clients.All.SendAsync("ReceiveSystem", mail, $"{isMatch}");
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
                        string dbMail = reader["mail"].ToString();
                        string dbNick = reader["nickname"].ToString();

                        if (string.Equals(dbMail, mail, StringComparison.OrdinalIgnoreCase))
                        {
                            userExists = true;
                        }

                        if (string.Equals(dbNick, user, StringComparison.OrdinalIgnoreCase))
                        {
                            nicknameTaken = true;
                        }
                    }
                }
            }

            if (!userExists && !nicknameTaken)
            {
                var service = new PasswordService();
                var result = service.HashPassword(password);

                var keyService = new KeyService();
                var keys = keyService.GenerateKeys();

                string insertSql =
                    "INSERT INTO user(mail, paswd, salt, public_key, nickname) VALUES(@mail, @pwd, @sal, @key, @nic)";

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
            else if (userExists)
            {
                await Clients.Caller.SendAsync("ReceiveSystem", "System", "User (mail) already exists.");
            }
            else if (nicknameTaken)
            {
                await Clients.Caller.SendAsync("ReceiveSystem", "System", "User (nickname) taken");
            }
        }
    }

    // Define a simple class to hold the data to send to client
    public class HistoryItem
    {
        public string Sender { get; set; }
        public string CipherText { get; set; } // The locked message
        public string IV { get; set; } // The lock specific settings
        public string MyEncryptedKey { get; set; } // The key to the message, locked with YOUR Public Key
        public DateTime Timestamp { get; set; }
    }

    public async Task<List<HistoryItem>> GetMyHistory(string myNickname)
    {
        var history = new List<HistoryItem>();
        string cs = _config.GetConnectionString("DefaultConnection");

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();

            // We join the 'messages' table with 'message_keys' table
            // We only grab rows where 'user_nickname' is YOU.
            string sql = @"
            SELECT m.sender_nickname, m.cipher_text, m.iv, m.timestamp, k.encrypted_session_key
            FROM messages m
            JOIN message_keys k ON m.id = k.message_id
            WHERE k.user_nickname = @me
            ORDER BY m.timestamp ASC";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@me", myNickname);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        history.Add(new HistoryItem
                        {
                            Sender = reader.GetString(reader.GetOrdinal("sender_nickname")),
                            CipherText = reader.GetString(reader.GetOrdinal("cipher_text")),
                            IV = reader.GetString(reader.GetOrdinal("iv")),
                            MyEncryptedKey = reader.GetString(reader.GetOrdinal("encrypted_session_key")),
                            Timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp"))
                        });
                    }
                }
            }
        }

        return history;
    }


    public async Task SendSecureMessage(string sender, string cipherText, string iv,
        Dictionary<string, string> keyBundle)
    {
        string cs = _config.GetConnectionString("DefaultConnection");

        using (var conn = new MySqlConnection(cs))
        {
            await conn.OpenAsync();
            using (var trans = await conn.BeginTransactionAsync())
            {
                try
                {
                    long messageId;
                    string sqlMsg =
                        "INSERT INTO messages(sender_nickname, cipher_text, iv) VALUES(@sender, @cipher, @iv); SELECT LAST_INSERT_ID();";

                    using (var cmd = new MySqlCommand(sqlMsg, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@sender", sender);
                        cmd.Parameters.AddWithValue("@cipher", cipherText);
                        cmd.Parameters.AddWithValue("@iv", iv);
                        messageId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    }

                    string sqlKey =
                        "INSERT INTO message_keys(message_id, user_nickname, encrypted_session_key) VALUES(@mid, @user, @key)";

                    using (var cmd = new MySqlCommand(sqlKey, conn, trans))
                    {
                        cmd.Parameters.Add("@mid", MySqlDbType.Int64);
                        cmd.Parameters.Add("@user", MySqlDbType.VarChar);
                        cmd.Parameters.Add("@key", MySqlDbType.Text);

                        foreach (var kvp in keyBundle)
                        {
                            cmd.Parameters["@mid"].Value = messageId;
                            cmd.Parameters["@user"].Value = kvp.Key;
                            cmd.Parameters["@key"].Value = kvp.Value;
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    await trans.CommitAsync();

                    foreach (var recipient in keyBundle.Keys)
                    {
                        string userKey = keyBundle[recipient];
                        await Clients.User(recipient)
                            .SendAsync("ReceiveSecureMessage", sender, cipherText, iv, userKey);
                    }
                }
                catch
                {
                    await trans.RollbackAsync();
                    throw;
                }
            }
        }
    }
}