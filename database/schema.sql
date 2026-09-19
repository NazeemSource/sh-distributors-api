CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

SET default_storage_engine = InnoDB;

CREATE TABLE `Companies` (
    `Id` char(36) NOT NULL,
    `Code` varchar(255) NOT NULL,
    `Name` varchar(255) NOT NULL,
    `ContactName` longtext NOT NULL,
    `Phone` longtext NOT NULL,
    `Address` longtext NOT NULL,
    `Active` tinyint(1) NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `Products` (
    `Id` char(36) NOT NULL,
    `CompanyId` char(36) NOT NULL,
    `Sku` varchar(255) NOT NULL,
    `Barcode` varchar(255) NOT NULL,
    `Name` varchar(255) NOT NULL,
    `Category` longtext NOT NULL,
    `SellingPrice` decimal(18,2) NOT NULL,
    `CostPrice` decimal(18,2) NOT NULL,
    `ReorderLevel` decimal(18,2) NOT NULL,
    `ExpiryDate` date NULL,
    `Active` tinyint(1) NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Products_Companies_CompanyId` FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `Shops` (
    `Id` char(36) NOT NULL,
    `CompanyId` char(36) NOT NULL,
    `Code` varchar(255) NOT NULL,
    `Name` varchar(255) NOT NULL,
    `ContactName` longtext NOT NULL,
    `Phone` longtext NOT NULL,
    `Address` longtext NOT NULL,
    `City` longtext NOT NULL,
    `CreditLimit` decimal(18,2) NOT NULL,
    `Active` tinyint(1) NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Shops_Companies_CompanyId` FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `StockIns` (
    `Id` char(36) NOT NULL,
    `CompanyId` char(36) NOT NULL,
    `StockInNumber` varchar(255) NOT NULL,
    `StockInDate` date NOT NULL,
    `StockTotal` decimal(18,2) NOT NULL,
    `PaymentStatus` varchar(255) NOT NULL,
    `Notes` longtext NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StockIns_Companies_CompanyId` FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `Users` (
    `Id` char(36) NOT NULL,
    `CompanyId` char(36) NULL,
    `Name` varchar(120) NOT NULL,
    `Username` varchar(80) NOT NULL,
    `PasswordHash` varchar(500) NOT NULL,
    `Role` varchar(30) NOT NULL,
    `Territory` varchar(120) NOT NULL,
    `Active` tinyint(1) NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Users_Companies_CompanyId` FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `InventoryTransactions` (
    `Id` char(36) NOT NULL,
    `ProductId` char(36) NOT NULL,
    `TransactionDate` datetime NOT NULL,
    `Type` longtext NOT NULL,
    `QuantityIn` decimal(18,2) NOT NULL,
    `QuantityOut` decimal(18,2) NOT NULL,
    `ReferenceType` longtext NOT NULL,
    `ReferenceId` char(36) NULL,
    `Notes` longtext NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InventoryTransactions_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `StockInPayments` (
    `Id` char(36) NOT NULL,
    `StockInId` char(36) NOT NULL,
    `PaymentDate` date NOT NULL,
    `PaidAmount` decimal(18,2) NOT NULL,
    `Method` longtext NOT NULL,
    `Reference` longtext NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StockInPayments_StockIns_StockInId` FOREIGN KEY (`StockInId`) REFERENCES `StockIns` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `StockInProducts` (
    `Id` char(36) NOT NULL,
    `StockInId` char(36) NOT NULL,
    `ProductId` char(36) NOT NULL,
    `Quantity` decimal(18,2) NOT NULL,
    `UnitCost` decimal(18,2) NOT NULL,
    `LineTotal` decimal(18,2) NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StockInProducts_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_StockInProducts_StockIns_StockInId` FOREIGN KEY (`StockInId`) REFERENCES `StockIns` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `Orders` (
    `Id` char(36) NOT NULL,
    `CompanyId` char(36) NOT NULL,
    `ShopId` char(36) NOT NULL,
    `SalesRepId` char(36) NOT NULL,
    `OrderNumber` varchar(255) NOT NULL,
    `OrderDate` date NOT NULL,
    `DeliveryDate` date NOT NULL,
    `OrderTotal` decimal(18,2) NOT NULL,
    `PaymentStatus` varchar(255) NOT NULL,
    `Status` longtext NOT NULL,
    `DeliveryAddress` longtext NOT NULL,
    `Notes` longtext NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Orders_Companies_CompanyId` FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Orders_Shops_ShopId` FOREIGN KEY (`ShopId`) REFERENCES `Shops` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Orders_Users_SalesRepId` FOREIGN KEY (`SalesRepId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `Cheques` (
    `Id` char(36) NOT NULL,
    `ShopId` char(36) NOT NULL,
    `OrderId` char(36) NULL,
    `ChequeNumber` varchar(255) NOT NULL,
    `BankName` longtext NOT NULL,
    `Amount` decimal(18,2) NOT NULL,
    `ChequeDate` date NOT NULL,
    `Status` varchar(255) NOT NULL,
    `RemindBeforeDays` int NOT NULL,
    `Notes` longtext NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Cheques_Orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Cheques_Shops_ShopId` FOREIGN KEY (`ShopId`) REFERENCES `Shops` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `OrderPayments` (
    `Id` char(36) NOT NULL,
    `OrderId` char(36) NOT NULL,
    `PaymentDate` date NOT NULL,
    `PaidAmount` decimal(18,2) NOT NULL,
    `Method` longtext NOT NULL,
    `Reference` longtext NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrderPayments_Orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `OrderProducts` (
    `Id` char(36) NOT NULL,
    `OrderId` char(36) NOT NULL,
    `ProductId` char(36) NOT NULL,
    `Quantity` decimal(18,2) NOT NULL,
    `FreeIssueQuantity` decimal(18,2) NOT NULL,
    `UnitPrice` decimal(18,2) NOT NULL,
    `LineSubtotal` decimal(18,2) NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime NULL,
    `CreatedAt` datetime NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrderProducts_Orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_OrderProducts_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX `IX_Cheques_ChequeNumber` ON `Cheques` (`ChequeNumber`);

CREATE INDEX `IX_Cheques_OrderId` ON `Cheques` (`OrderId`);

CREATE INDEX `IX_Cheques_ShopId_ChequeDate` ON `Cheques` (`ShopId`, `ChequeDate`);

CREATE INDEX `IX_Cheques_Status_ChequeDate` ON `Cheques` (`Status`, `ChequeDate`);

CREATE UNIQUE INDEX `IX_Companies_Code` ON `Companies` (`Code`);

CREATE INDEX `IX_Companies_Name` ON `Companies` (`Name`);

CREATE INDEX `IX_InventoryTransactions_ProductId_TransactionDate` ON `InventoryTransactions` (`ProductId`, `TransactionDate`);

CREATE INDEX `IX_OrderPayments_OrderId` ON `OrderPayments` (`OrderId`);

CREATE UNIQUE INDEX `IX_OrderProducts_OrderId_ProductId` ON `OrderProducts` (`OrderId`, `ProductId`);

CREATE INDEX `IX_OrderProducts_ProductId` ON `OrderProducts` (`ProductId`);

CREATE INDEX `IX_Orders_CompanyId` ON `Orders` (`CompanyId`);

CREATE UNIQUE INDEX `IX_Orders_OrderNumber` ON `Orders` (`OrderNumber`);

CREATE INDEX `IX_Orders_PaymentStatus` ON `Orders` (`PaymentStatus`);

CREATE INDEX `IX_Orders_SalesRepId_OrderDate` ON `Orders` (`SalesRepId`, `OrderDate`);

CREATE INDEX `IX_Orders_ShopId_OrderDate` ON `Orders` (`ShopId`, `OrderDate`);

CREATE UNIQUE INDEX `IX_Products_Barcode` ON `Products` (`Barcode`);

CREATE UNIQUE INDEX `IX_Products_CompanyId_Sku` ON `Products` (`CompanyId`, `Sku`);

CREATE INDEX `IX_Products_Name` ON `Products` (`Name`);

CREATE UNIQUE INDEX `IX_Shops_CompanyId_Code` ON `Shops` (`CompanyId`, `Code`);

CREATE INDEX `IX_Shops_Name` ON `Shops` (`Name`);

CREATE INDEX `IX_StockInPayments_StockInId` ON `StockInPayments` (`StockInId`);

CREATE INDEX `IX_StockInProducts_ProductId` ON `StockInProducts` (`ProductId`);

CREATE UNIQUE INDEX `IX_StockInProducts_StockInId_ProductId` ON `StockInProducts` (`StockInId`, `ProductId`);

CREATE INDEX `IX_StockIns_CompanyId_StockInDate` ON `StockIns` (`CompanyId`, `StockInDate`);

CREATE INDEX `IX_StockIns_PaymentStatus` ON `StockIns` (`PaymentStatus`);

CREATE UNIQUE INDEX `IX_StockIns_StockInNumber` ON `StockIns` (`StockInNumber`);

CREATE INDEX `IX_Users_CompanyId` ON `Users` (`CompanyId`);

CREATE UNIQUE INDEX `IX_Users_Username` ON `Users` (`Username`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260919160139_InitialBusinessSchema', '10.0.9');

COMMIT;

