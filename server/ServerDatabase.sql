CREATE DATABASE IF NOT EXISTS uchat;
USE uchat;

CREATE TABLE IF NOT EXISTS user(
    id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    mail varchar(100) NOT NULL UNIQUE,
    paswd varchar(256) NOT NULL,
    salt varchar(256) NOT NULL,
    public_key TEXT NOT NULL,
    nickname varchar(42) NOT NULL UNIQUE,
    pfp varchar(200) UNIQUE
);

CREATE TABLE IF NOT EXISTS chats(
    id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    type ENUM('private', 'group', 'channel') NOT NULL    
);

CREATE TABLE IF NOT EXISTS chat_member(
    usr_id INT NOT NULL,
    chat_id INT NOT NULL,
    status ENUM('owner', 'member', 'admin', 'muted', 'banned') DEFAULT 'member',
    PRIMARY KEY (usr_id, chat_id),
    FOREIGN KEY (usr_id) REFERENCES user(id) ON DELETE CASCADE,
    FOREIGN KEY (chat_id) REFERENCES chats(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS messages(
    id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    chat_id INT NOT NULL,
    sender_id INT NOT NULL,
    cipher_text MEDIUMTEXT NOT NULL,
    iv TEXT NOT NULL,
    send_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (sender_id) REFERENCES user(id),
    FOREIGN KEY (chat_id) REFERENCES chats(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS message_keys(
    id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
    message_id INT NOT NULL,
    recipient_id INT NOT NULL,
    encrypted_session_key TEXT NOT NULL,
    FOREIGN KEY (message_id) REFERENCES messages(id) ON DELETE CASCADE,
    FOREIGN KEY (recipient_id) REFERENCES user(id) ON DELETE CASCADE
);
