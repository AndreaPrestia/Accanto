import appConfig from '../app.config';

function loadConfig(variant: string | undefined): any {
  const original = process.env.APP_VARIANT;
  if (variant === undefined) {
    delete process.env.APP_VARIANT;
  } else {
    process.env.APP_VARIANT = variant;
  }
  try {
    return (appConfig as any)({ config: {} });
  } finally {
    if (original === undefined) delete process.env.APP_VARIANT;
    else process.env.APP_VARIANT = original;
  }
}

describe('app.config variant resolution', () => {
  it('production: bundle id senza suffisso, scheme "accanto"', () => {
    const c = loadConfig(undefined);
    expect(c.name).toBe('Accanto');
    expect(c.scheme).toBe('accanto');
    expect(c.ios.bundleIdentifier).toBe('app.accanto.mobile');
    expect(c.android.package).toBe('app.accanto.mobile');
    expect(c.extra.variant).toBe('production');
  });

  it('preview: bundle id .preview, scheme accanto.preview', () => {
    const c = loadConfig('preview');
    expect(c.name).toBe('Accanto (Preview)');
    expect(c.scheme).toBe('accanto.preview');
    expect(c.ios.bundleIdentifier).toBe('app.accanto.mobile.preview');
    expect(c.android.package).toBe('app.accanto.mobile.preview');
    expect(c.extra.variant).toBe('preview');
  });

  it('development: bundle id .dev, scheme accanto.dev', () => {
    const c = loadConfig('development');
    expect(c.name).toBe('Accanto (Dev)');
    expect(c.scheme).toBe('accanto.dev');
    expect(c.ios.bundleIdentifier).toBe('app.accanto.mobile.dev');
    expect(c.android.package).toBe('app.accanto.mobile.dev');
    expect(c.extra.variant).toBe('development');
  });

  it('alias "dev" mappa a development', () => {
    const c = loadConfig('dev');
    expect(c.extra.variant).toBe('development');
    expect(c.scheme).toBe('accanto.dev');
  });

  it('valore sconosciuto cade in production', () => {
    const c = loadConfig('staging');
    expect(c.extra.variant).toBe('production');
    expect(c.scheme).toBe('accanto');
  });

  it('associatedDomains punta ad accanto.care', () => {
    const c = loadConfig(undefined);
    expect(c.ios.associatedDomains).toEqual(['applinks:accanto.care']);
  });

  it('Android intent filter autoVerify=true su host accanto.care', () => {
    const c = loadConfig(undefined);
    const filter = c.android.intentFilters[0];
    expect(filter.autoVerify).toBe(true);
    expect(filter.data[0]).toEqual({ scheme: 'https', host: 'accanto.care' });
    expect(filter.category).toEqual(
      expect.arrayContaining(['BROWSABLE', 'DEFAULT'])
    );
  });
});
