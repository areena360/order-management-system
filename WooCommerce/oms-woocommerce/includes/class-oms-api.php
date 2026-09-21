<?php
defined('ABSPATH') || exit;

final class OMS_Woo_API {
    public static function local_url(string $url): bool {
        return defined('OMS_WOO_ALLOW_LOCAL_HTTP') && OMS_WOO_ALLOW_LOCAL_HTTP && wp_get_environment_type() === 'local' &&
            wp_parse_url($url, PHP_URL_SCHEME) === 'http' && in_array(wp_parse_url($url, PHP_URL_HOST), array('localhost', '127.0.0.1', '[::1]'), true);
    }
    public static function config(): array { return (array) get_option('oms_woo_config', array()); }
    public static function save(array $config): void { update_option('oms_woo_config', $config, false); }
    public static function connected(): bool {
        $c = self::config();
        return !empty($c['connection_id']) && !empty($c['secret']);
    }
    public static function request(string $path, ?array $body = null, bool $device = false) {
        $c = self::config();
        $base = $c['api_url'] ?? '';
        if (wp_parse_url($base, PHP_URL_SCHEME) !== 'https' && !self::local_url($base)) { return new WP_Error('oms_https', 'OMS requires HTTPS (or explicitly enabled loopback development).'); }
        $headers = array('Content-Type' => 'application/json', 'Accept' => 'application/json');
        if ($device) { $headers['X-OMS-Device-Secret'] = $c['pending_secret'] ?? ''; }
        elseif (!empty($c['secret'])) {
            $headers['Authorization'] = 'Bearer ' . $c['secret'];
            $headers['X-OMS-Connection'] = (string) ($c['connection_id'] ?? 0);
        }
        $args = array('method' => $body === null ? 'GET' : 'POST', 'headers' => $headers,
            'timeout' => 25, 'redirection' => 0, 'sslverify' => true, 'limit_response_size' => 1048576);
        if ($body !== null) { $args['body'] = wp_json_encode($body); }
        // Safe HTTP rejects private/internal targets and redirects cannot leak credentials.
        $url = rtrim($base, '/') . '/integrations/woocommerce/' . ltrim($path, '/');
        $response = self::local_url($url) ? wp_remote_request($url, $args) : wp_safe_remote_request($url, $args);
        if (is_wp_error($response)) { return new WP_Error('oms_network', 'OMS connection failed. Check HTTPS/network and retry.'); }
        $status = wp_remote_retrieve_response_code($response);
        if ($status < 200 || $status >= 300) {
            return new WP_Error('oms_http', 'OMS returned HTTP ' . $status . '. Check OMS authorization, defaults and logs.', array('status' => $status));
        }
        if ($status === 204) { return array(); }
        $data = json_decode(wp_remote_retrieve_body($response), true);
        return is_array($data) ? $data : new WP_Error('oms_response', 'OMS returned an invalid response.');
    }
    public static function log(int $order_id, string $result, string $message): void {
        $logs = (array) get_option('oms_woo_logs', array());
        array_unshift($logs, array('at' => gmdate('c'), 'order' => $order_id, 'result' => $result, 'message' => $message));
        update_option('oms_woo_logs', array_slice($logs, 0, 100), false);
        if (function_exists('wc_get_logger')) {
            wc_get_logger()->info($result . ' order ' . $order_id . ': ' . $message, array('source' => 'oms-connector'));
        }
    }
}
