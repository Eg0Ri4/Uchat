CREATE DATABASE IF NOT EXISTS UChat;
USE UChat;

CREATE TABLE IF NOT EXISTS user(
	id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
	mail varchar(100) NOT NULL,
	paswd varchar(256) NOT NULL,
	salt varchar(512) NOT NULL,
	public_key varchar(512) NOT NULL,
	nickname varchar(42) NOT NULL PRIMARY KEY,
	pfp varchar(200) PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS chats(
	id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
	type ENUM('private', 'group', 'channel') NOT NULL	
);

CREATE TABLE IF NOT EXISTS chat_member(
	usr_id INT NOT NULL,
	chat_id INT NOT NULL,
	PRIMARY KEY (usr_id, chat_id),
    FOREIGN KEY (usr_id) REFERENCES user(id),
    FOREIGN KEY (chat_id) REFERENCES chats(id),
	status ENUM('owner', 'member', 'admin', 'muted')
);

CREATE TABLE IF NOT EXISTS messages(
	usr_id INT NOT NULL,
	chat_id INT NOT NULL,
	PRIMARY KEY (usr_id, chat_id),
    FOREIGN KEY (usr_id) REFERENCES user(id),
    FOREIGN KEY (chat_id) REFERENCES chats(id),
	send_time TIMESTAMP NOT NULL,
	last_changed TIMESTAMP
);
