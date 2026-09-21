<?php
// CLI-only setup/test utility for the isolated artifacts/ local demo. Never deploy this file.
if (PHP_SAPI !== 'cli') { exit(1); }
$repo = dirname(__DIR__);
$root = $repo . '/artifacts/wordpress-local/wordpress';
$mode = $argv[1] ?? 'install';
$settings = json_decode(file_get_contents($repo . '/artifacts/wordpress-local.json'), true, 512, JSON_THROW_ON_ERROR);
if ($mode === 'install') {
    $db = new mysqli('127.0.0.1', 'root', $settings['rootPassword'], '', 3308);
    $db->query('CREATE DATABASE IF NOT EXISTS oms_wordpress_local CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
    $pass = $db->real_escape_string($settings['dbPassword']);
    $db->query("CREATE USER IF NOT EXISTS 'oms_wp'@'localhost' IDENTIFIED BY '$pass'");
    $db->query("GRANT ALL ON oms_wordpress_local.* TO 'oms_wp'@'localhost'");
    $db->close();
    if (!file_exists($root . '/wp-config.php')) {
        $config = "<?php\n";
        foreach (array('DB_NAME'=>'oms_wordpress_local','DB_USER'=>'oms_wp','DB_PASSWORD'=>$settings['dbPassword'], 'DB_HOST'=>'127.0.0.1:3308',
            'DB_CHARSET'=>'utf8mb4','DB_COLLATE'=>'', 'WP_HOME'=>'http://localhost:8088', 'WP_SITEURL'=>'http://localhost:8088',
            'WP_ENVIRONMENT_TYPE'=>'local', 'OMS_WOO_ALLOW_LOCAL_HTTP'=>true, 'DISABLE_WP_CRON'=>true, 'WP_HTTP_BLOCK_EXTERNAL'=>true,
            'WP_ACCESSIBLE_HOSTS'=>'localhost,127.0.0.1', 'AUTOMATIC_UPDATER_DISABLED'=>true, 'WP_DEBUG'=>false, 'FS_METHOD'=>'direct') as $key=>$value) {
            $config .= 'define(' . var_export($key,true) . ', ' . var_export($value,true) . ");\n";
        }
        foreach (array('AUTH_KEY','SECURE_AUTH_KEY','LOGGED_IN_KEY','NONCE_KEY','AUTH_SALT','SECURE_AUTH_SALT','LOGGED_IN_SALT','NONCE_SALT') as $key) {
            $config .= "define('$key', '" . bin2hex(random_bytes(32)) . "');\n";
        }
        $config .= "\$table_prefix = 'wp_';\nif (!defined('ABSPATH')) define('ABSPATH', __DIR__ . '/');\nrequire_once ABSPATH . 'wp-settings.php';\n";
        file_put_contents($root . '/wp-config.php', $config);
    }
    if (!is_dir($root . '/wp-content/mu-plugins')) mkdir($root . '/wp-content/mu-plugins');
    file_put_contents($root . '/wp-content/mu-plugins/oms-local-demo.php', "<?php\n// Isolated test store: suppress all outgoing email.\nadd_filter('pre_wp_mail', '__return_true');\n");
    define('WP_INSTALLING', true);
}
$_SERVER['HTTP_HOST'] = 'localhost:8088'; $_SERVER['SERVER_NAME'] = 'localhost'; $_SERVER['SERVER_PORT'] = '8088';
$_SERVER['REQUEST_URI'] = '/'; $_SERVER['REQUEST_METHOD'] = 'GET';
require $root . '/wp-load.php';
require_once ABSPATH . 'wp-admin/includes/plugin.php';
if ($mode === 'enable-test-payment') {
    if (wp_get_environment_type() !== 'local') throw new RuntimeException('Test payment setup requires the local environment.');
    $cod = get_option('woocommerce_cod_settings', array());
    $cod['enabled'] = 'yes';
    $cod['title'] = 'Cash on delivery';
    $cod['description'] = 'Local test order: no online payment is collected.';
    $cod['instructions'] = 'Local test order: no online payment is collected.';
    $cod['enable_for_methods'] = array();
    $cod['enable_for_virtual'] = 'yes';
    update_option('woocommerce_cod_settings', $cod);
    $gateway = WC()->payment_gateways()->payment_gateways()['cod'] ?? null;
    if (!$gateway || $gateway->enabled !== 'yes' || !$gateway->is_available()) throw new RuntimeException('Cash on delivery availability check failed.');
    echo "Cash on delivery enabled and available for local checkout. No online payment is collected.\n";
    exit;
}
if ($mode === 'install') {
    require_once ABSPATH . 'wp-admin/includes/upgrade.php';
    if (!is_blog_installed()) wp_install('OMS Local Test Store', $settings['adminUser'], 'demo@example.test', false, '', $settings['adminPassword']);
    $result = activate_plugin('woocommerce/woocommerce.php');
    if (is_wp_error($result)) throw new RuntimeException($result->get_error_message());
    $result = activate_plugin('oms-woocommerce/oms-woocommerce.php');
    if (is_wp_error($result)) throw new RuntimeException($result->get_error_message());
    update_option('woocommerce_allow_tracking', 'no');
    update_option('woocommerce_onboarding_profile', array('completed'=>true, 'skipped'=>true));
    update_option('woocommerce_store_address', 'Local test address');
    update_option('woocommerce_currency', 'USD');
    echo "WordPress and WooCommerce installed; connector activated. No email sent.\n";
    exit;
}
if (!class_exists('OMS_Woo_API')) throw new RuntimeException('Connector is not active.');
if ($mode === 'prepare-original') {
    if (wp_get_environment_type() !== 'local') throw new RuntimeException('Local environment required.');
    $c = OMS_Woo_API::config();
    if (OMS_Woo_API::connected()) {
        if (($c['api_url'] ?? '') === 'http://localhost:52984/api') { echo "Original OMS already connected.\n"; exit; }
        if (($c['api_url'] ?? '') !== 'http://localhost:5511/api') throw new RuntimeException('Unexpected existing destination; no change made.');
        $result = OMS_Woo_API::request('disconnect', array());
        if (is_wp_error($result)) throw new RuntimeException('Demo revocation failed; connection retained.');
    }
    $c = array('api_url'=>'http://localhost:52984/api', 'web_url'=>'http://localhost:4200', 'pending_secret'=>bin2hex(random_bytes(32)));
    OMS_Woo_API::save($c);
    delete_option('oms_woo_scan_state');
    foreach (array('oms_woo_send','oms_woo_scan') as $hook) as_unschedule_all_actions($hook, null, 'oms-woo');
    update_option('oms_woo_updates_cursor', 0, false);
    update_option('oms_woo_reconcile_from', time(), false);
    delete_option('oms_woo_sync_request');
    $result = OMS_Woo_API::request('authorize/start', array('installationId'=>get_option('oms_woo_installation'), 'storeName'=>get_bloginfo('name'), 'storeUrl'=>home_url(), 'tokenHash'=>hash('sha256',$c['pending_secret']), 'pluginVersion'=>OMS_WOO_VERSION));
    if (is_wp_error($result)) throw new RuntimeException($result->get_error_message());
    $c['device_id']=$result['deviceId']; $c['approval_code']=$result['code']; $c['expires_at']=$result['expiresAt'];
    OMS_Woo_API::save($c);
    echo "Original OMS authorization ready. Open WordPress OMS Integration and click Open OMS authorization.\n";
    exit;
}
if ($mode === 'connect') {
    $demo = json_decode(file_get_contents($repo . '/artifacts/local-demo.json'), true, 512, JSON_THROW_ON_ERROR);
    $c = OMS_Woo_API::config();
    if (OMS_Woo_API::connected()) { echo "Store already connected.\n"; exit; }
    $token = bin2hex(random_bytes(32));
    $c = array('api_url'=>$demo['apiUrl'], 'web_url'=>$demo['frontendUrl'], 'pending_secret'=>$token);
    OMS_Woo_API::save($c);
    $start = OMS_Woo_API::request('authorize/start', array('installationId'=>get_option('oms_woo_installation'), 'storeName'=>get_bloginfo('name'), 'storeUrl'=>home_url(), 'tokenHash'=>hash('sha256',$token), 'pluginVersion'=>OMS_WOO_VERSION));
    if (is_wp_error($start)) throw new RuntimeException($start->get_error_message());
    $login = wp_remote_post($demo['apiUrl'] . '/auth/login', array('headers'=>array('Content-Type'=>'application/json'), 'body'=>wp_json_encode(array('email'=>$demo['customerEmail'], 'password'=>$demo['password'])), 'timeout'=>30));
    $loginData = json_decode(wp_remote_retrieve_body($login), true);
    if (empty($loginData['token'])) throw new RuntimeException('Demo OMS login failed.');
    $headers = array('Content-Type'=>'application/json','Authorization'=>'Bearer ' . $loginData['token']);
    $statuses = wp_remote_get($demo['apiUrl'] . '/lookups/by-type/1', array('headers'=>$headers));
    $status = json_decode(wp_remote_retrieve_body($statuses), true)[0]['id'];
    $approved = wp_remote_post($demo['apiUrl'] . '/integrations/woocommerce/authorize/approve', array('headers'=>$headers,'body'=>wp_json_encode(array('code'=>$start['code'], 'defaultGenderId'=>5, 'defaultMaterialId'=>12, 'defaultStatusId'=>$status))));
    if (wp_remote_retrieve_response_code($approved) !== 200) throw new RuntimeException('Demo authorization failed: ' . wp_remote_retrieve_body($approved));
    $poll = OMS_Woo_API::request('authorize/poll/' . $start['deviceId'], array(), true);
    if (is_wp_error($poll) || ($poll['state'] ?? '') !== 'approved') throw new RuntimeException('Device polling failed.');
    $c['secret']=$token; $c['connection_id']=$poll['connectionId']; unset($c['pending_secret']); OMS_Woo_API::save($c);
    update_option('oms_woo_reconcile_from',time()-120,false);
    echo "Real WordPress to OMS authorization completed (connection " . $poll['connectionId'] . ").\n";
} elseif ($mode === 'seed') {
    $ids = get_option('oms_demo_order_ids');
    if (!$ids) {
        $product = new WC_Product_Simple(); $product->set_name('Demo leather jacket'); $product->set_regular_price('123.45'); $product->set_sku('OMS-DEMO-001'); $product->save();
        $ids = array();
        for ($i=1; $i<=10; $i++) {
            $order = wc_create_order(); $order->add_product($product, 2);
            $order->set_address(array('first_name'=>'Demo','last_name'=>'Buyer ' . $i,'email'=>'buyer' . $i . '@example.test','phone'=>'0000000000','address_1'=>'Test delivery address','city'=>'Lahore','country'=>'PK'), 'billing');
            $order->set_payment_method('cod'); $order->set_status('processing'); $order->calculate_totals(); $order->save(); $ids[]=$order->get_id();
        }
        update_option('oms_demo_order_ids', $ids, false);
    }
    foreach ($ids as $id) OMS_Woo_Sync::send($id);
    echo 'Demo order IDs: ' . implode(', ', $ids) . "\n";
} elseif ($mode === 'auto-order') {
    if (!get_option('oms_demo_auto_order_id')) {
        $product = wc_get_product(wc_get_product_id_by_sku('OMS-DEMO-001'));
        $order = wc_create_order(); $order->add_product($product, 1);
        $order->set_address(array('first_name'=>'Automatic','last_name'=>'Queue Test','email'=>'auto@example.test','address_1'=>'Local test address','country'=>'PK'), 'billing');
        $order->set_status('processing'); $order->calculate_totals(); $order->save();
        update_option('oms_demo_auto_order_id', $order->get_id(), false);
    }
    echo 'Created event-only order ' . get_option('oms_demo_auto_order_id') . "; no direct send invoked.\n";
} elseif ($mode === 'verify-auto') {
    $order = wc_get_order((int) get_option('oms_demo_auto_order_id'));
    if (!$order || !$order->get_meta('_oms_order_id')) throw new RuntimeException('Event-only order is waiting for the background worker.');
    echo 'PASS: Automatic event and background worker delivered WooCommerce #' . $order->get_id() . ' to OMS #' . $order->get_meta('_oms_order_id') . ".\n";
} elseif ($mode === 'hpos') {
    update_option('woocommerce_custom_orders_table_data_sync_enabled', 'yes');
    $sync = wc_get_container()->get(\Automattic\WooCommerce\Internal\DataStores\Orders\DataSynchronizer::class);
    for ($i = 0; $i < 50; $i++) {
        $batch = $sync->get_next_batch_to_process(100);
        if (!$batch) break;
        $sync->process_batch($batch);
    }
    if ($sync->get_current_orders_pending_sync_count() > 0) throw new RuntimeException('HPOS sync still pending.');
    update_option('woocommerce_custom_orders_table_enabled', 'yes');
    if (get_option('woocommerce_custom_orders_table_enabled') !== 'yes') throw new RuntimeException('HPOS activation failed.');
    echo "Existing test orders synchronized; HPOS enabled for the next request.\n";
} elseif ($mode === 'failure-test') {
    $ids = (array) get_option('oms_demo_order_ids');
    $id = $ids[0]; $order = wc_get_order($id);
    $order->set_customer_note('Retry test ' . gmdate('c')); $order->save();
    $config = OMS_Woo_API::config(); $bad = $config; $bad['api_url'] = 'http://localhost:5599/api';
    OMS_Woo_API::save($bad);
    try {
        OMS_Woo_Sync::send($id, 8);
        if (!as_has_scheduled_action('oms_woo_send', array($id, 9), 'oms-woo')) throw new RuntimeException('Long outage retry was lost.');
        echo "PASS: API outage retains a durable retry after attempt eight.\n";
    } finally { OMS_Woo_API::save($config); }
    OMS_Woo_Sync::send($id, 9);
    $order = wc_get_order($id);
    if ($order->get_meta('_oms_sync_failed')) throw new RuntimeException('Recovery did not clear failure state.');
    echo "PASS: Recovery sends the order and clears its failure state.\n";
    $before = count((array) get_option('oms_woo_logs'));
    OMS_Woo_Sync::send($id, 10);
    if (count((array) get_option('oms_woo_logs')) !== $before) throw new RuntimeException('Unchanged order re-synced.');
    echo "PASS: Connector metadata does not trigger repeated unchanged sends.\n";
} elseif ($mode === 'tick') {
    OMS_Woo_Sync::tick();
    ActionScheduler_QueueRunner::instance()->run();
    echo "Reconciliation, outbound status/tracking and queue processed.\n";
} elseif ($mode === 'verify') {
    foreach ((array) get_option('oms_demo_order_ids') as $id) {
        $o = wc_get_order($id);
        if (!$o->get_meta('_oms_order_id')) throw new RuntimeException('Order ' . $id . ' has not synced.');
        echo 'Order ' . $id . ' -> OMS ' . $o->get_meta('_oms_order_id') . ' | ' . $o->get_status() . ' | tracking ' . $o->get_meta('_oms_tracking_number') . "\n";
    }
    echo 'HPOS: ' . (\Automattic\WooCommerce\Utilities\OrderUtil::custom_orders_table_usage_is_enabled() ? 'enabled' : 'disabled') . "\n";
}
