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
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
    //TODO: login and registararion (WIP)
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
                    if (reader.Read()) 
                    {
                        db_hash = reader.GetString(0);
                        db_salt = reader.GetString(1);
                    }
                    else
                    {
                        Console.WriteLine("User not found.");
                        return;
                    }
                }
            }
        }
        
        //check password
        var service = new PasswordService();
        bool isMatch = service.VerifyPassword(password, db_hash, db_salt);
        //TODO:terminatre connection after 3 attemtss (not here)
        await Clients.All.SendAsync("ReceiveMessage", mail, $"{isMatch}");
    }
    public async Task register(string mail, string password, string user)
    {
            var service = new PasswordService();
            var result = service.HashPassword(password);

            string cs = _config.GetConnectionString("DefaultConnection");
            bool userExists = false;
            bool nicknameTaken = false;

            using (var conn = new MySqlConnection(cs))
            {
                await conn.OpenAsync(); 
                
                //check mail if exists
                string checkSql = "SELECT 1 FROM user WHERE mail = @mail";
                using (var cmdCheck = new MySqlCommand(checkSql, conn))
                {
                    cmdCheck.Parameters.AddWithValue("@mail", mail);

                    var exists = await cmdCheck.ExecuteScalarAsync();
                    userExists = (exists != null);
                } 
                //check if username taken
                checkSql = "SELECT 1 FROM user WHERE nickname = @name";
                using (var cmdCheck = new MySqlCommand(checkSql, conn))
                {
                    cmdCheck.Parameters.AddWithValue("@name", user);

                    var exists = await cmdCheck.ExecuteScalarAsync();
                    nicknameTaken = (exists != null);
                } 
                if (!userExists && !nicknameTaken)
                {
                    string insertSql = "INSERT INTO user(mail, paswd, salt, public_key, nickname) VALUES(@mail, @pwd, @sal, @key, @nic)";
                    using (var cmdInsert = new MySqlCommand(insertSql, conn))
                    {
                        cmdInsert.Parameters.AddWithValue("@mail", mail);
                        cmdInsert.Parameters.AddWithValue("@pwd", result.HashBase64);
                        cmdInsert.Parameters.AddWithValue("@sal", result.SaltBase64);
                        cmdInsert.Parameters.AddWithValue("@nic", user);
                        cmdInsert.Parameters.AddWithValue("@key", "TODO:Private key");
                
                        await cmdInsert.ExecuteNonQueryAsync();
                    }
                    await Clients.Caller.SendAsync("ReceiveMessage", "System", "User created successfully.");
                }
                else if(userExists)
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "System", "User (mail) already exists.");
                }else if (nicknameTaken)
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "System", "User (nickname) taken");
                }
            }
    }
}