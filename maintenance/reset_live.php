<?php
declare(strict_types=1);

// One-time production maintenance. Run only through the manual workflow.
$base = '/home/cwebsite';
$source = __DIR__.'/products.json';
$rows = json_decode(file_get_contents($source), true, flags: JSON_THROW_ON_ERROR);
if (!is_array($rows) || count($rows) !== 178) throw new RuntimeException('Unexpected product manifest count.');
$seen = [];
foreach ($rows as $row) {
    foreach (['sku', 'barcode', 'name', 'category', 'cost', 'mrp'] as $key) {
        if (!array_key_exists($key, $row)) throw new RuntimeException("Missing product field: $key");
    }
    if (isset($seen[$row['sku']]) || trim((string)$row['name']) === '' || !is_numeric($row['cost']) || !is_numeric($row['mrp']) || $row['cost'] < 0 || $row['mrp'] < 0) throw new RuntimeException('Invalid product manifest.');
    $seen[$row['sku']] = true;
}

require $base.'/public_html/pos-api/vendor/autoload.php';
$app = require $base.'/public_html/pos-api/bootstrap/app.php';
$app->make(Illuminate\Contracts\Console\Kernel::class)->bootstrap();
$config = config('database.connections.master');
if (($config['driver'] ?? '') !== 'mysql' || empty($config['username'])) throw new RuntimeException('MySQL configuration unavailable.');
$database = 'cwebsite_shdistr';
$dsn = 'mysql:host='.$config['host'].';port='.$config['port'].';dbname='.$database.';charset=utf8mb4';
$pdo = new PDO($dsn, $config['username'], $config['password'], [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]);
$tables = ['Cheques', 'OrderPayments', 'OrderProducts', 'Orders', 'StockInPayments', 'StockInProducts', 'StockIns', 'InventoryTransactions', 'Products', 'Shops', 'OfflineReceipts'];
$counts = [];
foreach ($tables as $table) $counts[$table] = (int)$pdo->query("SELECT COUNT(*) FROM `$table`")->fetchColumn();
$counts['Reps'] = (int)$pdo->query("SELECT COUNT(*) FROM `Users` WHERE `Role` <> 'Admin'")->fetchColumn();
$counts['AdminsRetained'] = (int)$pdo->query("SELECT COUNT(*) FROM `Users` WHERE `Role` = 'Admin' AND `IsDeleted` = 0")->fetchColumn();
$counts['Users'] = (int)$pdo->query('SELECT COUNT(*) FROM `Users`')->fetchColumn();
$counts['Companies'] = (int)$pdo->query('SELECT COUNT(*) FROM `Companies`')->fetchColumn();
$companies = $pdo->query("SELECT `Id`, `Code`, `Name` FROM `Companies` WHERE `IsDeleted` = 0")->fetchAll();
$matches = array_values(array_filter($companies, static fn($c) => strcasecmp(trim($c['Name']), 'KIST') === 0 || strcasecmp(trim($c['Code']), 'KIST') === 0));
$adminCompanyLinks = count($matches) === 1 ? $pdo->prepare("SELECT COUNT(*) FROM `Users` WHERE `Role` = 'Admin' AND `CompanyId` IS NOT NULL AND `CompanyId` <> ?") : null;
if ($adminCompanyLinks) $adminCompanyLinks->execute([$matches[0]['Id']]);
$counts['AdminsLinkedToOtherCompanies'] = $adminCompanyLinks ? (int)$adminCompanyLinks->fetchColumn() : null;
$preview = ['tables' => $counts, 'companies' => array_map(static fn($c) => ['code' => $c['Code'], 'name' => $c['Name']], $companies), 'kistMatches' => count($matches), 'productsToImport' => count($rows)];
echo json_encode($preview, JSON_PRETTY_PRINT | JSON_THROW_ON_ERROR), PHP_EOL;
if (($argv[1] ?? '') === 'preview') exit(0);
if (($argv[1] ?? '') === 'verify') {
    if ($counts['Products'] !== count($rows) || $counts['AdminsRetained'] !== 1 || $counts['Users'] !== 1 || $counts['Companies'] !== 1 || count($matches) !== 1) throw new RuntimeException('Post-reset product, company, or user count mismatch.');
    foreach ($tables as $table) if ($table !== 'Products' && $counts[$table] !== 0) throw new RuntimeException("Post-reset records remain in $table.");
    echo "VERIFIED\n";
    exit(0);
}
if (($argv[1] ?? '') !== 'apply' || ($argv[2] ?? '') !== 'RESET-LIVE-178') throw new RuntimeException('Explicit apply confirmation required.');
if ($counts['AdminsRetained'] !== 1 || count($matches) !== 1 || $counts['AdminsLinkedToOtherCompanies'] !== 0) throw new RuntimeException('Expected exactly one active admin, one KIST company, and no admin linked to another company. No records changed.');

// A complete SQL backup is mandatory. If mysqldump is unavailable or fails, abort.
$backupDir = $base.'/apps/shdistrapi-shared/backups';
if (!is_dir($backupDir) && !mkdir($backupDir, 0700, true)) throw new RuntimeException('Cannot create backup directory.');
chmod($backupDir, 0700);
$stamp = gmdate('Ymd-His');
$backup = $backupDir.'/before-live-products-'.$stamp.'.sql';
$defaults = $backupDir.'/mysql-'.$stamp.'.cnf';
$escape = static fn($s) => str_replace(['\\', '"', "\n", "\r"], ['\\\\', '\\"', '\\n', '\\r'], (string)$s);
$optionFile = "[client]\n";
foreach (['host' => $config['host'], 'port' => $config['port'], 'user' => $config['username'], 'password' => $config['password']] as $key => $value) $optionFile .= $key.'="'.$escape($value)."\"\n";
file_put_contents($defaults, $optionFile);
chmod($defaults, 0600);
try {
    $command = 'mysqldump --defaults-extra-file='.escapeshellarg($defaults).' --single-transaction --quick --skip-lock-tables --no-tablespaces '.escapeshellarg($database).' --result-file='.escapeshellarg($backup).' 2>&1';
    exec($command, $output, $exitCode);
    if ($exitCode !== 0 || !is_file($backup) || filesize($backup) < 1000) {
        if (is_file($backup)) unlink($backup);
        throw new RuntimeException('SQL backup failed: '.implode(' ', array_slice($output, -3)));
    }
    chmod($backup, 0600);
} finally {
    unlink($defaults);
}

$pdo->beginTransaction();
try {
    foreach ($tables as $table) $pdo->exec("DELETE FROM `$table`");
    $pdo->exec("DELETE FROM `Users` WHERE `Role` <> 'Admin' OR `IsDeleted` = 1");
    $pdo->exec('DELETE FROM `BrandingSettings` WHERE `IsDeleted` = 1');
    $deleteCompanies = $pdo->prepare('DELETE FROM `Companies` WHERE `Id` <> ?');
    $deleteCompanies->execute([$matches[0]['Id']]);
    $insert = $pdo->prepare('INSERT INTO `Products` (`Id`,`CompanyId`,`Sku`,`Barcode`,`Name`,`Category`,`SellingPrice`,`CostPrice`,`ReorderLevel`,`ExpiryDate`,`Active`,`IsDeleted`,`DeletedAt`,`CreatedAt`,`UpdatedAt`) VALUES (?,?,?,?,?,?,?,?,0,NULL,1,0,NULL,NOW(6),NOW(6))');
    $companyId = $matches[0]['Id'];
    foreach ($rows as $row) {
        $uuid = sprintf('%s-%s-%s-%s-%s', bin2hex(random_bytes(4)), bin2hex(random_bytes(2)), bin2hex(random_bytes(2)), bin2hex(random_bytes(2)), bin2hex(random_bytes(6)));
        $insert->execute([$uuid, $companyId, $row['sku'], $row['barcode'], $row['name'], $row['category'], $row['mrp'], $row['cost']]);
    }
    $actual = (int)$pdo->query('SELECT COUNT(*) FROM `Products`')->fetchColumn();
    if ($actual !== count($rows)) throw new RuntimeException("Import count mismatch: $actual");
    $pdo->commit();
} catch (Throwable $error) {
    $pdo->rollBack();
    throw $error;
}
echo 'RESET COMPLETE. Backup: '.$backup.'; products: '.count($rows).PHP_EOL;
