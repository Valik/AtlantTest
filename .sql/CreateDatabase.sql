CREATE TABLE dbo.tbl_Wallet (
    Id INT IDENTITY(1,1) NOT NULL,
    BitcoindServerAddress NVARCHAR(256) COLLATE Latin1_General_CI_AI NOT NULL,
    BitcoindUser NVARCHAR(256) COLLATE Latin1_General_CI_AI NULL,
    BitcoindPassword NVARCHAR(256) COLLATE Latin1_General_CI_AI NULL,
    Balance DECIMAL(20, 8) NOT NULL,
    WalletPassphrase NVARCHAR(256) COLLATE Latin1_General_CI_AI NULL,
    CONSTRAINT PK_tbl_Wallet PRIMARY KEY CLUSTERED (Id),
)
GO

CREATE TABLE dbo.tbl_Transaction (
    Id INT IDENTITY(1,1) NOT NULL,
    TxId NVARCHAR(64) COLLATE Latin1_General_CS_AI NOT NULL,
    WalletId INT NOT NULL,
    Amount DECIMAL(20, 8) NOT NULL,
    Fee DECIMAL(20, 8) NOT NULL,
    Category BIT NOT NULL, -- 0 - receive; 1 - send
    Address NVARCHAR(35) COLLATE Latin1_General_CS_AI NOT NULL,
    ReceivedDateTime DATETIME2(7) NOT NULL,
    Confirmations INT NOT NULL,
    CONSTRAINT PK_tbl_Transaction PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_tbl_Transaction_WalletId FOREIGN KEY (WalletId) REFERENCES dbo.tbl_Wallet (id),
    CONSTRAINT CH_tbl_Transaction_Fee_Category CHECK ((Category = 0 AND Fee = 0 AND Amount >= 0) OR (Category = 1  AND Amount <= 0 AND Fee <= 0)),
    CONSTRAINT UQ_tbl_Transaction_TxId UNIQUE(TxId, WalletId)
)
GO

CREATE PROCEDURE [dbo].[sp_UpdateWalletBalance]
    @Id INT,
    @Balance DECIMAL(20, 8)
AS
BEGIN
    UPDATE dbo.tbl_Wallet SET Balance = @Balance
    WHERE Id = @Id
END
GO

CREATE TYPE dbo.type_Transaction
AS TABLE
(
    Id INT NULL,
    TxId NVARCHAR(64) COLLATE Latin1_General_CS_AI NOT NULL,
    Amount DECIMAL(20, 8) NOT NULL,
    Fee DECIMAL(20, 8) NOT NULL,
    Category BIT NOT NULL, -- 0 - receive; 1 - send
    Address NVARCHAR(35) COLLATE Latin1_General_CS_AI NOT NULL,
    ReceivedDateTime DATETIME2(7) NOT NULL,
    Confirmations INT NOT NULL
);
GO

CREATE PROCEDURE [dbo].[sp_InsertOrUpdateTransactions]
    @walletId INT,
    @transactions as dbo.type_Transaction READONLY
AS
BEGIN
    INSERT INTO dbo.tbl_Transaction (
        TxId,
        WalletId,
        Amount,
        Fee,
        Category,
        Address,
        ReceivedDateTime,
        Confirmations)
    SELECT
        TxId,
        @walletId,
        Amount,
        Fee,
        Category,
        Address,
        ReceivedDateTime,
        Confirmations
        FROM @transactions [source]
    WHERE [source].Id IS NULL

    UPDATE dbo.tbl_Transaction SET
        Confirmations = [source].Confirmations
    FROM @transactions [source]
    WHERE dbo.tbl_Transaction.Id = [source].Id AND dbo.tbl_Transaction.WalletId = @walletId

    UPDATE dbo.tbl_Wallet SET Balance = ISNULL(
        (SELECT SUM(Amount + IIF(Fee is NULL, 0, Fee)) -- for send transactions Amount, Fee are negative
         FROM dbo.tbl_Transaction WHERE WalletId = @walletId), 0)
    WHERE Id = @walletId
END
GO

CREATE PROCEDURE [dbo].[sp_SelectWallets]
AS
BEGIN
    SELECT
        Id,
        BitcoindServerAddress,
        BitcoindUser,
        BitcoindPassword,
        Balance,
        WalletPassphrase
    FROM dbo.tbl_Wallet
END
GO

CREATE PROCEDURE [dbo].[sp_SelectTransactions]
    @walletId INT
AS
BEGIN
    SELECT
        Id,
        TxId,
        Amount,
        Fee,
        Category,
        Address,
        ReceivedDateTime,
        Confirmations
    FROM dbo.tbl_Transaction
    WHERE WalletId = @walletId
END
GO

-- Please fill Wallet's details below.
-- Provided values as an example.
INSERT INTO
dbo.tbl_Wallet (
    BitcoindServerAddress,
    BitcoindUser,
    BitcoindPassword,
    Balance, -- Balance 
    WalletPassphrase)
VALUES
(
N'http://127.0.0.1:8332',   -- BitcoindServerAddress
N'user',                    -- BitcoindUser
N'pwd',                     -- BitcoindPassword
0,                          -- Balance will be updated during GetLast api call. 
N'1'                        -- WalletPassphrase
),
(
N'http://127.0.0.1:8333',   -- BitcoindServerAddress
N'user',                    -- BitcoindUser
N'pwd',                     -- BitcoindPassword
0,                          -- Balance will be updated during GetLast api call. 
NULL                       -- WalletPassphrase, leave NULL if it does not exist
)

/* 
 * PS.
 * I'm new in bitcoin.
 * I did't find a way to configure one instance of bitcoind to serve several wallets. Maybe it is my misunderstanding of HOT-Wallet term.
 * Current implementation relies on running several instances of bitcoind, one per wallet. (Through configuration I run them on different ports with -datadir param)
 */

