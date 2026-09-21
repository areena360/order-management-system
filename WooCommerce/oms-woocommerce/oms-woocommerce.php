<?php
/**
 * Plugin Name: OMS WooCommerce Connector
 * Description: Connect a WooCommerce store to OMS with authorized, queued order synchronization.
 * Version: 1.0.2
 * Requires at least: 6.5
 * Requires PHP: 8.1
 * Requires Plugins: woocommerce
 * WC requires at least: 8.2
 * Author: OMS
 * License: GPL-2.0-or-later
 */
defined('ABSPATH') || exit;
define('OMS_WOO_VERSION', '1.0.2');
require_once __DIR__ . '/includes/class-oms-api.php';
require_once __DIR__ . '/includes/class-oms-sync.php';
require_once __DIR__ . '/includes/class-oms-admin.php';

add_action('before_woocommerce_init', static function () {
    if (class_exists('Automattic\\WooCommerce\\Utilities\\FeaturesUtil')) {
        \Automattic\WooCommerce\Utilities\FeaturesUtil::declare_compatibility('custom_order_tables', __FILE__, true);
    }
});
register_activation_hook(__FILE__, static function () {
    if (!class_exists('WooCommerce')) { wp_die('OMS Connector requires WooCommerce. Activate WooCommerce first.'); }
    if (!get_option('oms_woo_installation')) { add_option('oms_woo_installation', wp_generate_uuid4(), '', false); }
});
register_deactivation_hook(__FILE__, static function () {
    if (function_exists('as_unschedule_all_actions')) {
        foreach (array('oms_woo_tick', 'oms_woo_send', 'oms_woo_scan') as $hook) {
            as_unschedule_all_actions($hook, null, 'oms-woo');
        }
    }
});
add_action('plugins_loaded', static function () {
    if (!class_exists('WooCommerce')) {
        add_action('admin_notices', static function () { echo '<div class="notice notice-error"><p>OMS Connector requires WooCommerce.</p></div>'; });
        return;
    }
    OMS_Woo_Sync::init();
    OMS_Woo_Admin::init();
});
