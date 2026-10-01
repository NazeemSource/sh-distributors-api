<?php
declare(strict_types=1);

// One-time cleanup: exact product name AND both prices must match.
$mode = $argv[1] ?? '';
if (!in_array($mode, ['preview', 'apply', 'verify'], true)) throw new RuntimeException('Use preview, apply, or verify.');
if ($mode === 'apply' && ($argv[2] ?? '') !== 'REMOVE-EXACT-PRICE-DUPLICATES') throw new RuntimeException('Explicit confirmation required.');

$base = '/home/cwebsite';
require $base.'/public_html/pos-api/vendor/autoload.php';
$app = require $base.'/public_html/pos-api/bootstrap/app.php';
$app->make(Illuminate\Contracts\Console\Kernel::class)->bootstrap();
$config = config('database.connections.master');
if (($config['driver'] ?? '') !== 'mysql' || empty($config['username'])) throw new RuntimeException('MySQL configuration unavailable.');
$database = 'cwebsite_shdistr';
$dsn = 'mysql:host='.$config['host'].';port='.$config['port'].';dbname='.$database.';charset=utf8mb4';
$pdo = new PDO($dsn, $config['username'], $config['password'], [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]);

$products = $pdo->query('SELECT `Id`,`CompanyId`,`Sku`,`Name`,`Category`,`CostPrice`,`SellingPrice` FROM `Products` WHERE `IsDeleted` = 0')->fetchAll();
$groups = [];
foreach ($products as $product) {
    // PHP string keys preserve exact spelling and spacing; a price difference keeps both rows.
    $key = implode("\0", [$product['CompanyId'], $product['Name'], $product['CostPrice'], $product['SellingPrice']]);
    $groups[$key][] = $product;
}
$references = $pdo->prepare('SELECT (SELECT COUNT(*) FROM `OrderProducts` WHERE `ProductId` = ?) + (SELECT COUNT(*) FROM `StockInProducts` WHERE `ProductId` = ?) + (SELECT COUNT(*) FROM `InventoryTransactions` WHERE `ProductId` = ?)');
$remove = [];
$blocked = [];
foreach ($groups as $group) {
    if (count($group) < 2) continue;
    foreach ($group as &$product) {
        $references->execute([$product['Id'], $product['Id'], $product['Id']]);
        $product['References'] = (int)$references->fetchColumn();
    }
    unset($product);
    usort($group, static fn($a, $b) => ($b['References'] <=> $a['References']) ?: ((int)($b['Category'] === 'KIST BEVERAGE') <=> (int)($a['Category'] === 'KIST BEVERAGE')) ?: strcmp($a['Sku'], $b['Sku']));
    if ($group[1]['References'] > 0) { $blocked[] = $group[0]['Name']; continue; }
    foreach (array_slice($group, 1) as $product) $remove[] = $product;
}
$summary = ['activeProducts' => count($products), 'duplicatesToRemove' => count($remove), 'blockedNames' => $blocked, 'namesToRemove' => array_column($remove, 'Name')];
echo json_encode($summary, JSON_PRETTY_PRINT | JSON_THROW_ON_ERROR), PHP_EOL;
if ($mode === 'preview') exit(0);
if ($mode === 'verify') {
    if ($remove || $blocked) throw new RuntimeException('Exact-name-and-price duplicates remain.');
    echo 'VERIFIED'.PHP_EOL;
    exit(0);
}
if (count($products) !== 178 || count($remove) !== 43 || $blocked) throw new RuntimeException('Live catalog differs from the reviewed 178 products / 43 duplicates. Nothing changed.');

// Preserve a private, complete SQL backup before touching production records.
$backupDir = $base.'/apps/shdistrapi-shared/backups';
if (!is_dir($backupDir) && !mkdir($backupDir, 0700, true)) throw new RuntimeException('Cannot create backup directory.');
chmod($backupDir, 0700);
$stamp = gmdate('Ymd-His');
$backup = $backupDir.'/before-product-dedupe-'.$stamp.'.sql';
$defaults = $backupDir.'/mysql-dedupe-'.$stamp.'.cnf';
$escape = static fn($s) => str_replace(['\\', '"', "\n", "\r"], ['\\\\', '\\"', '\\n', '\\r'], (string)$s);
$optionFile = "[client]\n";
foreach (['host' => $config['host'], 'port' => $config['port'], 'user' => $config['username'], 'password' => $config['password']] as $key => $value) $optionFile .= $key.'="'.$escape($value)."\"\n";
file_put_contents($defaults, $optionFile);
chmod($defaults, 0600);
try {
    $command = 'mysqldump --defaults-extra-file='.escapeshellarg($defaults).' --single-transaction --quick --skip-lock-tables --no-tablespaces --column-statistics=0 '.escapeshellarg($database).' --result-file='.escapeshellarg($backup).' 2>&1';
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
    $delete = $pdo->prepare('DELETE FROM `Products` WHERE `Id` = ? AND `IsDeleted` = 0');
    foreach ($remove as $product) {
        $references->execute([$product['Id'], $product['Id'], $product['Id']]);
        if ((int)$references->fetchColumn() !== 0) throw new RuntimeException('A duplicate became referenced. Rolled back.');
        $delete->execute([$product['Id']]);
        if ($delete->rowCount() !== 1) throw new RuntimeException('A duplicate changed before deletion. Rolled back.');
    }
    if ((int)$pdo->query('SELECT COUNT(*) FROM `Products` WHERE `IsDeleted` = 0')->fetchColumn() !== 135) throw new RuntimeException('Unexpected final product count. Rolled back.');
    $pdo->commit();
} catch (Throwable $error) {
    $pdo->rollBack();
    throw $error;
}
echo 'DEDUPE COMPLETE. Backup: '.$backup.'; removed: '.count($remove).'; active products: 135'.PHP_EOL;
