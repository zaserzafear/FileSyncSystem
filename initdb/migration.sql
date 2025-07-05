CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `Files` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `FilePath` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Revision` int NOT NULL,
    `Size` bigint NOT NULL,
    `Checksum` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ContentType` longtext CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_Files` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_Files_FilePath_Revision` ON `Files` (`FilePath`, `Revision`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20250706072444_Initial', '8.0.17');

COMMIT;

