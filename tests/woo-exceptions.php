<?php
// Isolated connector diagnostic tests; no WordPress installation or network access.
define('ABSPATH', __DIR__);
$options = array('oms_woo_config' => array('api_url' => 'https://oms.example.test/api', 'connection_id' => 1, 'secret' => 'test-secret'));
$mode = 'offline';
$hooks = array();
function get_option($key, $default = false) { global $options; return $options[$key] ?? $default; }
function update_option($key, $value, $autoload = false) { global $options; $options[$key] = $value; return true; }
function add_option($key, $value, $unused = '', $autoload = false) { global $options; if (isset($options[$key])) return false; $options[$key] = $value; return true; }
function delete_option($key) { global $options; unset($options[$key]); }
function wp_generate_uuid4() { return sprintf('00000000-0000-4000-8000-%012d', random_int(1, 999999999)); }
function wp_parse_url($url, $component = -1) { return parse_url($url, $component); }
function wp_json_encode($data) { return json_encode($data); }
function is_wp_error($value) { return $value instanceof WP_Error; }
class WP_Error { public function __construct(...$args) {} }
function wp_safe_remote_request($url, $args) {
    global $mode;
    if ($mode === 'throw') throw new RuntimeException('private-token');
    if ($mode === 'offline') return new WP_Error();
    if ($mode === 'concurrent') { OMS_Woo_API::log(2, 'failed', 'private message'); $mode = 'online'; }
    return array('status' => 202, 'body' => '{"recorded":true}');
}
function wp_remote_retrieve_response_code($response) { return $response['status']; }
function wp_remote_retrieve_body($response) { return $response['body']; }
function add_action($name, $callback, ...$args) { global $hooks; $hooks[$name] = $callback; }
require __DIR__ . '/../WooCommerce/oms-woocommerce/includes/class-oms-api.php';
require __DIR__ . '/../WooCommerce/oms-woocommerce/includes/class-oms-sync.php';
function check($condition, $name) { if (!$condition) throw new RuntimeException('FAIL: ' . $name); echo "PASS: $name\n"; }

OMS_Woo_API::log(1, 'failed', 'password=private-token');
$queued = get_option('oms_woo_error_queue');
check(count($queued) === 1 && !str_contains(json_encode($queued), 'private-token'), 'durable reports exclude raw failure messages');
OMS_Woo_API::flush_errors();
check(get_option('oms_woo_error_queue') === $queued, 'network failure preserves original event ID for retry');
check(!get_option('oms_woo_error_flush_lock'), 'failure releases flush lock');
$mode = 'concurrent';
OMS_Woo_API::flush_errors();
$remaining = get_option('oms_woo_error_queue');
check(count($remaining) === 1 && $remaining[0]['orderId'] === 2, 'successful batch removes only acknowledged IDs and preserves new reports');
OMS_Woo_API::flush_errors();
check(get_option('oms_woo_error_queue') === array(), 'accepted response drains queue');
OMS_Woo_Sync::init();
$mode = 'throw';
try { $hooks['oms_woo_tick'](); throw new LogicException('expected exception'); }
catch (RuntimeException $error) { check($error->getMessage() === 'private-token', 'scheduled callback preserves failure semantics'); }
check(get_option('oms_woo_error_queue')[0]['kind'] === 'exception', 'scheduled callback exception added to durable queue');
check(get_option('oms_woo_logs')[0]['message'] === 'Connector operation tick failed (RuntimeException).', 'callback report uses a safe exception type');
