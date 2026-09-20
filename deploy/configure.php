<?php
declare(strict_types=1);
umask(0077);
$base = '/home/cwebsite';
require $base.'/public_html/pos-api/vendor/autoload.php';
$app = require $base.'/public_html/pos-api/bootstrap/app.php';
$app->make(Illuminate\Contracts\Console\Kernel::class)->bootstrap();
$db = config('database.connections.master');
if (($db['driver'] ?? '') !== 'mysql' || empty($db['username'])) throw new RuntimeException('VPS MySQL configuration is missing.');
$serverDsn = 'mysql:host='.$db['host'].';port='.$db['port'].';charset=utf8mb4';
$pdo = new PDO($serverDsn, $db['username'], $db['password'], [PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION]);
$database = 'cwebsite_shdistr';
$pdo->exec('CREATE DATABASE IF NOT EXISTS `'.$database.'` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
function csq(string $value): string { return '"'.str_replace('"', '""', $value).'"'; }
$connection = 'Server='.csq((string)$db['host']).';Port='.(int)$db['port'].';Database='.csq($database).';User ID='.csq((string)$db['username']).';Password='.csq((string)$db['password']).';SslMode=Preferred';
$shared = $base.'/apps/shdistrapi-shared';
if (!is_dir($shared) && !mkdir($shared, 0700, true) && !is_dir($shared)) throw new RuntimeException('Cannot create shared directory.');
$settingsPath = $shared.'/appsettings.Production.json';
$existing = is_file($settingsPath) ? json_decode(file_get_contents($settingsPath), true, flags: JSON_THROW_ON_ERROR) : [];
$jwtKey = $existing['Jwt']['Key'] ?? bin2hex(random_bytes(48));
$seedPassword = $existing['Seed']['AdminPassword'] ?? getenv('SHDISTR_ADMIN_PASSWORD');
if (!is_string($seedPassword) || strlen($seedPassword) < 6) throw new RuntimeException('Production admin password is missing or too short.');
$settings = [
  'ConnectionStrings'=>['Default'=>$connection],
  'Database'=>['UseInMemory'=>false,'UseEnsureCreated'=>true],
  'Jwt'=>['Issuer'=>'Distributor.Api','Audience'=>'Distributor.Apps','Key'=>$jwtKey,'Hours'=>12],
  'Cors'=>['Origins'=>['https://dist.umigs.com','https://rep.umigs.com']],
  'Seed'=>['Enabled'=>true,'AdminUsername'=>'admin','AdminName'=>'Administrator - 01','AdminPassword'=>$seedPassword],
  'AllowedHosts'=>'shdistrapi.umigs.com;127.0.0.1;localhost'
];
file_put_contents($settingsPath, json_encode($settings, JSON_PRETTY_PRINT|JSON_THROW_ON_ERROR));
chmod($settingsPath, 0600);
$credentialPath = $shared.'/initial-admin.txt';
if (!is_file($credentialPath)) {
  file_put_contents($credentialPath, "Username: admin\nPassword: ".$seedPassword."\nChange this password after first login.\n");
  chmod($credentialPath, 0600);
}
echo "Private API configuration and database are ready.\n";
