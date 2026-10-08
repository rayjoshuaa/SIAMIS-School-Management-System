import logo from '../../assets/branding/siam-international-school-logo.png';
export function Brand() {
  return (
    <div className="shell-brand" aria-label="SIAMIS, Siam International School">
      <img src={logo} alt="" />
      <div>
        <p>SIAMIS</p>
        <span>Siam International School</span>
      </div>
    </div>
  );
}
