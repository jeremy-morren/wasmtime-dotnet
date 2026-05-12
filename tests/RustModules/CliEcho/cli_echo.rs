mod bindings {
    wit_bindgen::generate!({
        inline: r#"
            package wasmtime:cli-echo;

            interface api {
                list-files: func(dir: string) -> list<string>;
                get-env-vars: func() -> list<string>;
                get-args: func() -> list<string>;
                get-initial-cwd: func() -> option<string>;
                get-current-time: func() -> s64;
                get-monotonic-time: func() -> u64;
                get-random-u64: func() -> u64;
                resolve-addresses: func(name: string) -> list<string>;
            }

            world cli-echo {
                export api;
            }
        "#,
        generate_all,
    });
}

use std::net::{Ipv4Addr, Ipv6Addr};

use bindings::exports::wasmtime::cli_echo::api::Guest;

struct WasiP2Smoke;

impl Guest for WasiP2Smoke {
    fn list_files(dir: String) -> Vec<String> {
        list_files_impl(&dir)
    }

    fn get_env_vars() -> Vec<String> {
        let mut vars = wasi::cli::environment::get_environment()
            .into_iter()
            .map(|(name, value)| format!("{name}={value}"))
            .collect::<Vec<_>>();
        vars.sort();
        vars
    }

    fn get_args() -> Vec<String> {
        wasi::cli::environment::get_arguments()
    }

    fn get_initial_cwd() -> Option<String> {
        wasi::cli::environment::initial_cwd()
    }

    fn get_current_time() -> i64 {
        wasi::clocks::wall_clock::now().seconds as i64
    }

    fn get_monotonic_time() -> u64 {
        wasi::clocks::monotonic_clock::now()
    }

    fn get_random_u64() -> u64 {
        wasi::random::random::get_random_u64()
    }

    fn resolve_addresses(name: String) -> Vec<String> {
        resolve_addresses_impl(&name)
    }
}

bindings::export!(WasiP2Smoke with_types_in bindings);

fn list_files_impl(dir: &str) -> Vec<String> {
    let requested_dir = normalize_guest_path(dir);
    let preopens: Vec<(wasi::filesystem::types::Descriptor, String)> =
        wasi::filesystem::preopens::get_directories();
    let preopen_paths = preopens
        .iter()
        .map(|(_, path)| path.clone())
        .collect::<Vec<_>>();

    let mut selected: Option<(
        wasi::filesystem::types::Descriptor,
        String,
        String,
    )> = None;

    for (descriptor, preopen_path) in preopens {
        if let Some(relative_path) = strip_preopen_prefix(&requested_dir, &preopen_path) {
            let replace = selected
                .as_ref()
                .map(|(_, current_path, _)| preopen_path.len() > current_path.len())
                .unwrap_or(true);

            if replace {
                selected = Some((descriptor, preopen_path, relative_path));
            }
        }
    }

    let Some((base_descriptor, _, relative_path)) = selected else {
        let mut errors = vec![format!("error:no-preopen-for:{requested_dir}")];
        errors.extend(
            preopen_paths
                .into_iter()
                .map(|path| format!("preopen:{path}")),
        );
        return errors;
    };

    let target_descriptor = if relative_path.is_empty() {
        base_descriptor
    } else {
        match base_descriptor.open_at(
            wasi::filesystem::types::PathFlags::empty(),
            &relative_path,
            wasi::filesystem::types::OpenFlags::DIRECTORY,
            wasi::filesystem::types::DescriptorFlags::READ,
        ) {
            Ok(descriptor) => descriptor,
            Err(error) => {
                return vec![format!(
                    "error:open-at:{requested_dir}:{}",
                    error.name()
                )]
            }
        }
    };

    let stream = match target_descriptor.read_directory() {
        Ok(stream) => stream,
        Err(error) => {
            return vec![format!(
                "error:read-directory:{requested_dir}:{}",
                error.name()
            )]
        }
    };

    let mut entries = Vec::new();
    loop {
        match stream.read_directory_entry() {
            Ok(Some(entry)) => entries.push(entry.name),
            Ok(None) => break,
            Err(error) => {
                return vec![format!(
                    "error:read-directory-entry:{requested_dir}:{}",
                    error.name()
                )]
            }
        }
    }

    entries.sort();
    entries
}

fn resolve_addresses_impl(name: &str) -> Vec<String> {
    let network = wasi::sockets::instance_network::instance_network();
    let stream = match wasi::sockets::ip_name_lookup::resolve_addresses(&network, name) {
        Ok(stream) => stream,
        Err(error) => return vec![format!("error:resolve-addresses:{}", error.name())],
    };

    let pollable = stream.subscribe();
    let mut addresses = Vec::new();

    loop {
        match stream.resolve_next_address() {
            Ok(Some(address)) => addresses.push(format_ip_address(address)),
            Ok(None) => break,
            Err(wasi::sockets::network::ErrorCode::WouldBlock) => {
                let _ = wasi::io::poll::poll(&[&pollable]);
            }
            Err(error) => {
                return vec![format!("error:resolve-next-address:{}", error.name())]
            }
        }
    }

    addresses
}

fn normalize_guest_path(path: &str) -> String {
    let trimmed = path.trim();
    if trimmed.is_empty() {
        return ".".to_string();
    }

    let replaced = trimmed.replace('\\', "/");
    if replaced == "/" {
        return replaced;
    }

    let absolute = replaced.starts_with('/');
    let parts = replaced
        .split('/')
        .filter(|segment| !segment.is_empty() && *segment != ".")
        .collect::<Vec<_>>();

    if parts.is_empty() {
        return if absolute {
            "/".to_string()
        } else {
            ".".to_string()
        };
    }

    let joined = parts.join("/");
    if absolute {
        format!("/{joined}")
    } else {
        joined
    }
}

fn strip_preopen_prefix(path: &str, preopen_path: &str) -> Option<String> {
    let normalized_preopen = normalize_guest_path(preopen_path);

    if normalized_preopen == "." || normalized_preopen == "/" {
        return Some(path.trim_start_matches('/').to_string());
    }

    if path == normalized_preopen {
        return Some(String::new());
    }

    path.strip_prefix(&(normalized_preopen + "/"))
        .map(ToOwned::to_owned)
}

fn format_ip_address(address: wasi::sockets::network::IpAddress) -> String {
    match address {
        wasi::sockets::network::IpAddress::Ipv4((a, b, c, d)) => {
            Ipv4Addr::new(a, b, c, d).to_string()
        }
        wasi::sockets::network::IpAddress::Ipv6((a, b, c, d, e, f, g, h)) => {
            Ipv6Addr::new(a, b, c, d, e, f, g, h).to_string()
        }
    }
}
